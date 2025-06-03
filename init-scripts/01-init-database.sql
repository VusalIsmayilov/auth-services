-- This script runs when PostgreSQL container starts for the first time

-- Create development database
CREATE DATABASE authservice_db_dev;

-- Grant privileges to the user
GRANT ALL PRIVILEGES ON DATABASE authservice_db TO authservice_user;
GRANT ALL PRIVILEGES ON DATABASE authservice_db_dev TO authservice_user;

-- Create extensions if needed
\c authservice_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

\c authservice_db_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Log completion
\echo 'Database initialization completed successfully!'