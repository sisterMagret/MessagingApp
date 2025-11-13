# MessagingApp Web UI

A comprehensive web interface for the MessagingApp that provides all the messaging and group chat features through an intuitive single-page application.

## Features

### 🔐 Authentication

- **User Registration**: Create new accounts with email and password
- **User Login**: Secure authentication with JWT tokens
- **Session Management**: Automatic token storage and logout functionality

### 📨 Messaging

- **Send Messages**: Send direct messages to other users or group messages
- **Inbox Management**: View all received messages with pagination
- **Message Status**: Mark messages as read/unread with visual indicators
- **File Attachments**: Attach files to messages (coming soon)
- **Real-time Updates**: Live message notifications via SignalR

### 👥 Group Chat

- **Create Groups**: Start new group chats with custom names and descriptions
- **Group Management**: View group details, members, and roles
- **Member Management**: Add and remove members (admin/owner permissions required)
- **Group Messaging**: Send messages to all group members

### 💰 Subscription Management

- **Feature Subscriptions**: View active and expired subscriptions
- **Available Plans**: Browse subscription options for premium features
- **Subscription Status**: Real-time status of all feature subscriptions
- **Cost Overview**: Monthly cost breakdown for active subscriptions

### 📁 File Management

- **File Upload**: Upload files with drag-and-drop or file picker
- **File Download**: Download previously uploaded files
- **File Organization**: View and manage all uploaded files (coming soon)

## Getting Started

### Prerequisites

1. .NET 9.0 or later
2. SQL Server (LocalDB or full instance)
3. Modern web browser with JavaScript enabled

### Running the Application

1. **Start the API Server**:

   ```bash
   cd src/Api
   dotnet run
   ```

2. **Open the Web Interface**:
   Navigate to `http://localhost:5250` in your browser

### First Time Setup

1. **Register a New Account**:
   - Click the "Register" tab
   - Enter your email and password
   - Click "Register" to create your account

2. **Login**:
   - Switch to the "Login" tab
   - Enter your credentials
   - Click "Login" to access the dashboard

## Using the Interface

### Navigation

The application uses a tabbed interface with four main sections:

- **Messages**: Send and receive direct and group messages
- **Groups**: Create and manage group chats
- **Subscriptions**: View and manage feature subscriptions
- **Files**: Upload and manage files

### Messaging Workflow

1. Click "New Message" to compose a message
2. Choose between "Direct Message" or "Group Message"
3. Enter recipient details or select a group
4. Type your message content
5. Optionally attach a file
6. Click "Send Message"

### Group Chat Workflow

1. Click "Create Group" to start a new group
2. Enter group name and description
3. Click "Create Group"
4. Use "Add Member" to invite users (enter user IDs)
5. Send messages to the entire group

### Real-time Features

- **Live Notifications**: Receive instant notifications for new messages
- **Auto-refresh**: Messages and group updates happen automatically
- **Connection Status**: Visual indicators for network connectivity

## API Integration

The UI integrates with the following API endpoints:

### Authentication

- `POST /api/auth/register` - User registration
- `POST /api/auth/login` - User login
- `GET /api/auth/me` - Get current user info

### Messages

- `POST /api/messages` - Send message
- `GET /api/messages/inbox` - Get inbox messages
- `POST /api/messages/{id}/read` - Mark message as read

### Groups

- `GET /api/groups` - Get user's groups
- `POST /api/groups` - Create new group
- `GET /api/groups/{id}` - Get group details
- `POST /api/groups/{groupId}/members/{userId}` - Add member
- `DELETE /api/groups/{groupId}/members/{userId}` - Remove member

### Subscriptions

- `GET /api/subscriptions/summary` - Get subscription summary
- `GET /api/subscriptions/plans` - Get available plans
- `GET /api/subscriptions/has-feature/{feature}` - Check feature access

### Files

- `POST /api/files/upload` - Upload file
- `GET /api/files/download` - Download file
- `DELETE /api/files` - Delete file

## Technology Stack

### Frontend

- **HTML5**: Semantic markup and structure
- **CSS3**: Modern styling with animations and responsive design
- **Vanilla JavaScript**: No framework dependencies, pure JS
- **SignalR Client**: Real-time communication

### Backend Integration

- **REST API**: RESTful endpoints for all operations
- **JWT Authentication**: Secure token-based authentication
- **SignalR Hub**: Real-time messaging and notifications
- **File Upload**: Multipart form data handling

## Features in Development

### Upcoming Features

- [ ] Advanced file management with previews
- [ ] Message search and filtering
- [ ] Group chat moderation tools
- [ ] Push notifications
- [ ] Dark mode theme
- [ ] Mobile app version
- [ ] Voice and video calls
- [ ] Message reactions and emojis
- [ ] File sharing in groups
- [ ] Advanced subscription management

### Known Limitations

- File attachments in messages are in development
- User search functionality not yet implemented
- Group discovery features coming soon
- Payment integration for subscriptions pending

## Browser Compatibility

### Supported Browsers

- ✅ Chrome 90+
- ✅ Firefox 88+
- ✅ Safari 14+
- ✅ Edge 90+

### Required Features

- ES6+ JavaScript support
- CSS Grid and Flexbox
- Fetch API
- WebSocket support (for SignalR)

## Troubleshooting

### Common Issues

1. **Login Issues**:
   - Ensure API server is running
   - Check browser console for errors
   - Verify credentials are correct

2. **Real-time Features Not Working**:
   - Check network connection
   - Verify SignalR is enabled in browser
   - Refresh the page to reconnect

3. **File Upload Issues**:
   - Check file size limits
   - Verify file type is supported
   - Ensure proper permissions

### Debug Mode

Open browser developer tools (F12) to see:

- Network requests and responses
- JavaScript console errors
- SignalR connection status

## Security Considerations

- All API calls use JWT authentication
- Passwords are never stored in localStorage
- File uploads are validated server-side
- CORS is properly configured
- XSS protection through HTML escaping

## Support and Feedback

For issues, suggestions, or contributions:

1. Check the browser console for errors
2. Verify API server is running and accessible
3. Test with different browsers if issues persist
4. Report bugs with detailed reproduction steps
