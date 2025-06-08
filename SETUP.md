# AuthService Role-Based Access Control Setup Guide

## 🚀 Quick Start Guide

### 1. **Start Services**

```bash
# Start Keycloak and database
docker-compose up -d

# Run AuthService
dotnet run
```

### 2. **Access Points**
- **AuthService API**: http://localhost:5000
- **Swagger Documentation**: http://localhost:5000/swagger
- **Keycloak Admin**: http://localhost:8081 (admin/admin)
- **API Health**: http://localhost:5000/health

### 3. **Bootstrap Initial Admin**

**Option A: Via AuthService (Recommended)**
```bash
# Check system status
curl http://localhost:5000/api/admin/status

# Create first admin user
curl -X POST http://localhost:5000/api/admin/bootstrap \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "SecurePassword123!"
  }'
```

**Option B: Via Keycloak (if configured)**
- Keycloak will auto-import the realm with admin@authservice.com/admin123

## 🔐 Role Management

### Available Roles:
- **Admin**: Full system access and user management
- **User1**: Specific access permissions for type 1 users  
- **User2**: Different access permissions for type 2 users

### Admin Endpoints (Require Admin Role):
```bash
# Assign role to user
POST /api/role/assign
{
  "userId": 2,
  "role": "User1",
  "notes": "Initial role assignment"
}

# Get all users with roles
GET /api/role/users

# Get users by role
GET /api/role/users/Admin

# Get role statistics
GET /api/role/statistics
```

### User Endpoints (Any authenticated user):
```bash
# Get my role and permissions
GET /api/role/my-role

# Check specific permission
GET /api/role/check-permission/user:manage
```

## 🧪 Testing the System

### 1. **Create Test Users**
```bash
# Register users
curl -X POST http://localhost:5000/api/auth/register/email \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user1@example.com",
    "password": "Password123!"
  }'

curl -X POST http://localhost:5000/api/auth/register/email \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user2@example.com", 
    "password": "Password123!"
  }'
```

### 2. **Quick Role Assignment (Development Only)**
*Only works when no admin exists*
```bash
curl -X POST http://localhost:5000/api/admin/quick-assign-role \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user1@example.com",
    "role": "User1"
  }'
```

### 3. **Login and Test Access**
```bash
# Login as admin
curl -X POST http://localhost:5000/api/auth/login/email \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "SecurePassword123!"
  }'

# Use the returned accessToken in Authorization header
curl -X GET http://localhost:5000/api/role/users \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## 🔧 Configuration

### Keycloak Integration
Update `appsettings.json`:
```json
{
  "Keycloak": {
    "Authority": "http://localhost:8081/realms/authservice",
    "ClientId": "authservice-client", 
    "Audience": "account"
  }
}
```

### Authentication Schemes
The system supports dual authentication:
- **Internal JWT**: Default for AuthService-generated tokens
- **Keycloak JWT**: For tokens from Keycloak

## 📊 System Status

Check the system status anytime:
```bash
curl http://localhost:5000/api/admin/status
```

Response includes:
- Total users count
- Role distribution
- Whether bootstrap is required
- Available roles and permissions

## 🛡️ Security Features

- **Role Hierarchy**: Admin > User1/User2
- **Permission-Based Access**: Fine-grained control
- **Audit Trail**: Complete role assignment history
- **Token Rotation**: Refresh token security
- **Email Verification**: User validation system
- **Rate Limiting**: OTP and verification controls

## 🐛 Troubleshooting

### Common Issues:

1. **"Bootstrap not allowed"**
   - Admin user already exists
   - Use proper role management endpoints

2. **"Keycloak connection failed"**  
   - Check Keycloak is running on port 8081
   - Verify realm "authservice" exists

3. **"Authorization failed"**
   - Check token in Authorization header
   - Verify user has required role
   - Token may be expired (15 min lifetime)

### Reset System:
```bash
# Reset database (removes all users and roles)
dotnet ef database drop
dotnet ef database update
```