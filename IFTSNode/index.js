require('dotenv').config();
const express = require('express');
const mysql = require('mysql2');
const redis = require('redis');
const util = require('util');

const app = express();
const port = process.env.PORT || 3000;

// Configurazione MySQL
const db = mysql.createConnection({
    host: process.env.MYSQL_HOST,
    user: process.env.MYSQL_USER,
    password: process.env.MYSQL_PASSWORD,
    database: process.env.MYSQL_DATABASE
});

db.connect((err) => {
    if (err) {
        console.error('Errore di connessione a MySQL:', err);
        process.exit(1);
    }
    console.log('Connesso a MySQL');
});


redisClient.on('error', (err) => {
    console.error('Errore di connessione a Redis:', err);
});

redisClient.get = util.promisify(redisClient.get);

async function ensureRedisConnection() {
    if (!redisClient.isOpen) {
        redisClient = redis.createClient({
            host: process.env.REDIS_HOST,
            port: process.env.REDIS_PORT
        });
        redisClient.on('error', (err) => console.error('Redis error:', err));
        await redisClient.connect();
    }
}

// Endpoint per ottenere il catalogo
app.get('/catalog', async (req, res) => {
    await ensureRedisConnection();
    try {
        // Verifica se i dati sono presenti nella cache Redis
        const cacheResults = await redisClient.get('catalog-node');

        if (cacheResults) {
            return res.json(JSON.parse(cacheResults));
        }

        // Se non presente in cache, recupera i dati da MySQL
        db.query('SELECT * FROM Catalog', (err, results) => {
            if (err) {
                console.error('Errore durante la query su MySQL:', err);
                return res.status(500).json({ error: 'Errore del server' });
            }

            // Salva i risultati in Redis con una scadenza di 1 ora (3600 secondi)
            redisClient.set('catalog-node', JSON.stringify(results));

            // Rispondi con i dati recuperati da MySQL
            res.json(results);
        });
    } catch (error) {
        console.error('Errore:', error);
        res.status(500).json({ error: 'Errore del server' });
    }
});

// Avvia il server
app.listen(port, () => {
    console.log(`Server in ascolto su http://localhost:${port}`);
});