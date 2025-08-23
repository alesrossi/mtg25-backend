-- Create the identity database if it doesn't exist
SELECT 'CREATE DATABASE identity_int'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'identity_int');