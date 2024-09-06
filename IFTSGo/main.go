package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/go-redis/redis/v8"
	_ "github.com/go-sql-driver/mysql"
	"github.com/joho/godotenv"
)

var (
	db          *sql.DB
	redisClient *redis.Client
	ctx         = context.Background()
)

func main() {
	// Carica le variabili di ambiente
	if err := godotenv.Load(); err != nil {
		log.Fatal("Errore durante il caricamento del file .env")
	}

	// Configurazione MySQL
	dsn := fmt.Sprintf("%s:%s@tcp(%s)/%s",
		os.Getenv("MYSQL_USER"),
		os.Getenv("MYSQL_PASSWORD"),
		os.Getenv("MYSQL_HOST"),
		os.Getenv("MYSQL_DATABASE"),
	)

	var err error
	db, err = sql.Open("mysql", dsn)
	if err != nil {
		log.Fatal("Errore di connessione a MySQL:", err)
	}

	if err = db.Ping(); err != nil {
		log.Fatal("Errore durante il ping a MySQL:", err)
	}
	defer db.Close()

	// Configurazione Redis
	redisClient = redis.NewClient(&redis.Options{
		Addr:     os.Getenv("REDIS_HOST"),
		Password: os.Getenv("REDIS_PASSWORD"),
		DB:       0,
	})

	if _, err := redisClient.Ping(ctx).Result(); err != nil {
		log.Fatal("Errore di connessione a Redis:", err)
	}
	defer redisClient.Close()

	// Configura il router
	router := gin.Default()

	// Definisci gli endpoint
	router.GET("/catalog", getCatalog)

	// Avvia il server
	port := os.Getenv("PORT")
	router.Run(":" + port)
}

func getCatalog(c *gin.Context) {
	// Verifica se i dati sono presenti nella cache Redis
	cachedCatalog, err := redisClient.Get(ctx, "catalog-go").Result()
	if err == redis.Nil {
		// Chiave non presente in Redis, quindi recupera i dati da MySQL
		rows, err := db.Query("SELECT name, description FROM Catalog")
		if err != nil {
			log.Println("Errore durante la query su MySQL:", err)
			c.JSON(http.StatusInternalServerError, gin.H{"error": "Errore del server"})
			return
		}
		defer rows.Close()

		var catalog []map[string]interface{}

		for rows.Next() {
			var name, description string

			if err := rows.Scan(&name, &description); err != nil {
				log.Println("Errore durante la scansione dei risultati:", err)
				c.JSON(http.StatusInternalServerError, gin.H{"error": "Errore del server"})
				return
			}

			item := map[string]interface{}{
				"name":        name,
				"description": description,
			}

			catalog = append(catalog, item)
		}

		// Serializza i risultati in JSON
		catalogJSON, err := json.Marshal(catalog)
		if err != nil {
			log.Println("Errore durante la serializzazione dei risultati:", err)
			c.JSON(http.StatusInternalServerError, gin.H{"error": "Errore del server"})
			return
		}

		// Memorizza i risultati nella cache Redis con una scadenza di 1 ora
		err = redisClient.Set(ctx, "catalog-go", catalogJSON, time.Hour).Err()
		if err != nil {
			log.Println("Errore durante il salvataggio nella cache Redis:", err)
		}

		// Rispondi con i dati recuperati da MySQL
		c.JSON(http.StatusOK, catalog)
	} else if err != nil {
		log.Println("Errore durante l'accesso alla cache Redis:", err)
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Errore del server"})
	} else {
		// Rispondi con i dati presenti nella cache Redis
		var catalog []map[string]interface{}
		if err := json.Unmarshal([]byte(cachedCatalog), &catalog); err != nil {
			log.Println("Errore durante la deserializzazione della cache Redis:", err)
			c.JSON(http.StatusInternalServerError, gin.H{"error": "Errore del server"})
			return
		}

		c.JSON(http.StatusOK, catalog)
	}
}
