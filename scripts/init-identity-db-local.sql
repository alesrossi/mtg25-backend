-- Create the identity database if it doesn't exist (for local development)
SELECT 'CREATE DATABASE identity'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'identity')\gexec
