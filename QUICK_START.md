# 🚀 Quick Start & Testing Guide

## **Start the App (60 seconds)**

```bash
# 1. Ensure SQL Server is running
docker ps | grep sqlserver
# If not running:
docker start sqlserver

# 2. Start the app
cd /Users/admin/Documents/MessagingApp
dotnet run --project src/Api

# 3. Open Swagger
# Visit: http://localhost:5250/swagger
```

---

## **🧪 Quick Test Sequence**

### **Test 1: Register Users (2 min)**

**User 1 - Alice**

```
POST /api/auth/register
{
  "email": "alice@example.com",
  "password": "Alice@12345"
}
```

💾 Save: `ALICE_TOKEN` from response

**User 2 - Bob**

```
POST /api/auth/register
{
  "email": "bob@example.com",
  "password": "Bob@12345"
}
```

💾 Save: `BOB_TOKEN` from response

---

### **Test 2: Send Free Message (Alice → Bob)**

```
POST /api/messages
Authorization: Bearer ALICE_TOKEN

{
  "content": "Hi Bob!",
  "receiverId": 2,
  "fileUrl": null,
  "voiceUrl": null
}
```

✅ **Expected:** Message created

---

### **Test 3: Bob Receives Message**

```
GET /api/messages/inbox
Authorization: Bearer BOB_TOKEN
```

✅ **Expected:** Shows Alice's message

---

### **Test 4: Feature Gating Demo**

**Try voice message WITHOUT subscription:**

```
POST /api/messages
Authorization: Bearer ALICE_TOKEN

{
  "content": "Voice test",
  "receiverId": 2,
  "voiceUrl": "https://example.com/voice.mp3",
  "fileUrl": null
}
```

❌ **Expected:** 401 - "Voice messaging is not included"

---

### **Test 5: Purchase Feature**

```
POST /api/payments/purchase
Authorization: Bearer ALICE_TOKEN

{
  "feature": 1,
  "months": 1,
  "paymentToken": "tok_visa",
  "amount": 2.99
}
```

✅ **Expected:** 200 OK - Transaction ID returned

---

### **Test 6: Now Voice Message Works**

```
POST /api/messages
Authorization: Bearer ALICE_TOKEN

{
  "content": "Voice test",
  "receiverId": 2,
  "voiceUrl": "https://example.com/voice.mp3",
  "fileUrl": null
}
```

✅ **Expected:** 200 OK - Message with voice URL

---

### **Test 7: Create Group (Alice Needs GroupChat Feature)**

```
POST /api/payments/purchase
Authorization: Bearer ALICE_TOKEN

{
  "feature": 4,
  "months": 1,
  "paymentToken": "tok_visa",
  "amount": 9.99
}
```

Then:

```
POST /api/groups
Authorization: Bearer ALICE_TOKEN

{
  "name": "Development Team",
  "description": "Our team"
}
```

✅ **Expected:** Group created with ID=1

---

### **Test 8: Add Bob to Group**

```
POST /api/groups/1/members
Authorization: Bearer ALICE_TOKEN

{
  "userId": 2
}
```

✅ **Expected:** 200 OK

---

### **Test 9: Group Message**

```
POST /api/messages
Authorization: Bearer ALICE_TOKEN

{
  "content": "Welcome to the team!",
  "receiverId": null,
  "groupId": 1,
  "fileUrl": null,
  "voiceUrl": null
}
```

✅ **Expected:** Message in group

---

## **📋 API Quick Reference**

| Endpoint | Method | Auth | Purpose |
|----------|--------|------|---------|
| `/auth/register` | POST | No | Register user |
| `/auth/login` | POST | No | Login user |
| `/auth/me` | GET | Yes | Get current user |
| `/messages` | POST | Yes | Send message |
| `/messages/inbox` | GET | Yes | Get inbox |
| `/messages/{id}/read` | POST | Yes | Mark as read |
| `/groups` | POST | Yes | Create group |
| `/groups` | GET | Yes | List groups |
| `/groups/{id}/members` | POST | Yes | Add member |
| `/groups/{id}/members/{uid}` | DELETE | Yes | Remove member |
| `/subscriptions/has-feature/{f}` | GET | Yes | Check feature |
| `/subscriptions/my-subscriptions` | GET | Yes | List subscriptions |
| `/payments/purchase` | POST | Yes | Buy feature |

---

## **🔍 Common Issues & Solutions**

### **Issue: JWT Key Error**

```
IDX10720: key size must be greater than: '256' bits
```

**Solution:** Key in `appsettings.json` must be 32+ characters

```json
"Jwt": {
  "Key": "this-is-a-very-secure-secret-key-that-is-long-enough"
}
```

### **Issue: SQL Server Not Running**

```bash
# Check status
docker ps | grep sqlserver

# Start if stopped
docker start sqlserver

# Or create new
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=P@ssw0rd2024" \
  -p 1433:1433 --name sqlserver -d \
  mcr.microsoft.com/mssql/server:2022-latest
```

### **Issue: Port 5250 Already In Use**

```bash
# Find process on port 5250
lsof -i :5250

# Kill it
kill -9 <PID>

# Or change port in launchSettings.json
```

---

## **🎓 Key Concepts**

### **Authentication**

- Register: Creates new User with hashed password
- Login: Validates password, returns 7-day JWT token
- Token contains UserId + Email as claims
- Each request must include: `Authorization: Bearer {token}`

### **Messages**

- Direct: `receiverId` set, `groupId` null
- Group: `groupId` set, `receiverId` null
- Feature-gated: voice/file require active subscription
- Read status tracked with `LastNotifiedAt` for alerts

### **Subscriptions**

- Feature-specific (user can have multiple)
- Min 1 month, max limited by business rules
- Auto-checked before allowing feature use
- Email alerts trigger after 30 min if unread

### **Email Alerts**

- EmailAlertWorker runs every minute
- Finds unread messages older than 30 minutes
- Won't send more than once per 30-min window
- Uses `LastNotifiedAt` to track last email

---

## **📊 Performance Tips**

- Inbox uses pagination (default 50 items)
- Message queries include related data (EF Core `.Include()`)
- Subscriptions cached in user service calls
- Email alert worker uses background service (doesn't block API)

---

## **🔐 Security Reminders**

- ✅ All endpoints except auth require JWT
- ✅ JWT tokens expire in 7 days
- ✅ Passwords hashed with PasswordHasher (bcrypt-equivalent)
- ✅ Feature access verified before message send
- ✅ SQL Server uses strong connection string with TrustServerCertificate

**For production:**

- [ ] Change JWT secret
- [ ] Change SQL Server password
- [ ] Enable HTTPS enforcement
- [ ] Set up real email service (not console)
- [ ] Configure CORS properly

---

## **🚀 Deployment**

### **Local Testing** (Current)

```bash
dotnet run --project src/Api
# http://localhost:5250/swagger
```

### **Production Build**

```bash
dotnet publish -c Release --no-self-contained
# Deploy to Azure App Service, AWS, etc.
```

### **Docker Deployment**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 as build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "Api.dll"]
```

---

## **📞 Support**

See `IMPLEMENTATION_SUMMARY.md` for:

- Full architecture details
- Database schema
- SOLID principles explanation
- Complete API documentation
- Troubleshooting guide

---

**Ready to submit!** ✅

All features working, database seeded, API documented. Good luck! 🎉
