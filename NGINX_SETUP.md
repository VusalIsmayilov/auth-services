# Nginx Integration Setup

## 🚀 Quick Start

```bash
# Stop any running services first
docker-compose down

# Start all services with Nginx
docker-compose up -d

# Check service status
docker-compose ps
```

## 🌐 Access Points

After starting with Nginx, access your services at:

- **AuthService API**: http://localhost (port 80)
- **Swagger Documentation**: http://localhost/swagger
- **API Health Check**: http://localhost/health
- **Keycloak Admin**: http://localhost:8081
- **PgAdmin**: http://localhost:8080

## 🔧 Nginx Features

### Load Balancing & Reverse Proxy
- Routes requests to appropriate backend services
- Health checks for backend availability
- Connection pooling with keepalive

### Security Features
- Rate limiting on authentication endpoints (5 req/s)
- Rate limiting on API endpoints (10 req/s)
- Security headers (X-Frame-Options, XSS Protection, etc.)
- Request size limits

### Performance Optimizations
- Gzip compression for text content
- Response buffering
- Connection keepalive
- Efficient worker configuration

## 📊 Monitoring

### Nginx Logs
```bash
# View access logs
docker-compose logs nginx

# View real-time logs
docker-compose logs -f nginx

# Access log files directly
docker exec authservice_nginx tail -f /var/log/nginx/access.log
```

### Health Checks
```bash
# Check all service health
curl http://localhost/health

# Check individual services
docker-compose ps
```

## 🛠️ Configuration

### Port Mapping
- **Port 80**: AuthService API (through Nginx)
- **Port 8081**: Keycloak (through Nginx) 
- **Port 8080**: PgAdmin (through Nginx)
- **Port 5432**: PostgreSQL (direct)
- **Port 6379**: Redis (direct)

### Environment Variables
```bash
# Customize Nginx ports
export NGINX_HTTP_PORT=80
export NGINX_KEYCLOAK_PORT=8081
export NGINX_PGADMIN_PORT=8080
```

## 🔒 Rate Limiting

### Authentication Endpoints
- `/api/auth/login`, `/api/auth/register`, `/api/auth/reset-password`
- Limit: 5 requests/second per IP
- Burst: 10 requests allowed

### General API Endpoints
- `/api/*` (all other API endpoints)
- Limit: 10 requests/second per IP
- Burst: 20 requests allowed

## 🐛 Troubleshooting

### Common Issues

1. **Port 80 already in use**
   ```bash
   # Check what's using port 80
   sudo lsof -i :80
   
   # Use different port
   export NGINX_HTTP_PORT=8000
   docker-compose up -d
   ```

2. **Nginx configuration errors**
   ```bash
   # Test Nginx config
   docker exec authservice_nginx nginx -t
   
   # Reload configuration
   docker exec authservice_nginx nginx -s reload
   ```

3. **Backend services not responding**
   ```bash
   # Check backend health
   docker-compose ps
   docker-compose logs authservice
   docker-compose logs keycloak
   ```

### Service Dependencies
Nginx will only start after:
- AuthService is healthy (`/health` endpoint responds)
- Keycloak is healthy (`/health/ready` endpoint responds)

## 📈 Performance Notes

### Production Considerations
- Enable HTTPS/TLS termination at Nginx level
- Implement proper SSL certificates
- Configure additional security headers
- Set up log rotation
- Monitor resource usage

### Scaling
- Add more AuthService instances to docker-compose
- Update Nginx upstream configuration
- Implement session affinity if needed