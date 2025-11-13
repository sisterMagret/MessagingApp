// Application State
let currentUser = null;
let authToken = null;
let currentPage = 1;
let currentTab = 'messages';
let groups = [];
let messages = [];
let signalRConnection = null;

// API Configuration
// Auto-detect the API base URL or use localhost:5250 as fallback
const API_BASE = window.location.hostname === 'localhost' && window.location.port === '5250' 
    ? '/api' 
    : 'http://localhost:5250/api';
const PAGE_SIZE = 10;

// Initialize Application
document.addEventListener('DOMContentLoaded', function() {
    initializeApp();
    setupEventListeners();
});

// ==================== INITIALIZATION ====================

function initializeApp() {
    // Check for existing auth token
    const token = localStorage.getItem('authToken');
    const user = localStorage.getItem('currentUser');
    
    if (token && user) {
        authToken = token;
        currentUser = JSON.parse(user);
        showDashboard();
        initializeSignalR();
    } else {
        showAuthSection();
    }
}

function setupEventListeners() {
    // Auth tabs
    document.getElementById('login-tab').addEventListener('click', () => showAuthForm('login'));
    document.getElementById('register-tab').addEventListener('click', () => showAuthForm('register'));
    
    // Auth forms
    document.getElementById('login-form-element').addEventListener('submit', handleLogin);
    document.getElementById('register-form-element').addEventListener('submit', handleRegister);
    
    // Navigation tabs
    document.querySelectorAll('.nav-tab').forEach(tab => {
        tab.addEventListener('click', (e) => switchTab(e.target.dataset.tab));
    });
    
    // Logout
    document.getElementById('logout-btn').addEventListener('click', handleLogout);
    
    // Messages
    document.getElementById('compose-message-btn').addEventListener('click', () => showModal('compose-modal'));
    document.getElementById('compose-form').addEventListener('submit', handleSendMessage);
    document.getElementById('message-type').addEventListener('change', handleMessageTypeChange);
    document.getElementById('refresh-messages').addEventListener('click', loadMessages);
    document.getElementById('prev-page').addEventListener('click', () => changePage(-1));
    document.getElementById('next-page').addEventListener('click', () => changePage(1));
    
    // Groups
    document.getElementById('create-group-btn').addEventListener('click', () => showModal('group-modal'));
    document.getElementById('create-group-form').addEventListener('submit', handleCreateGroup);
    document.getElementById('back-to-groups').addEventListener('click', showGroupsList);
    document.getElementById('add-member-btn').addEventListener('click', handleAddMember);
    
    // Subscriptions
    document.getElementById('refresh-subscriptions').addEventListener('click', loadSubscriptions);
    
    // Files
    document.getElementById('upload-file-btn').addEventListener('click', toggleFileUpload);
    document.getElementById('file-upload-form').addEventListener('submit', handleFileUpload);
    document.getElementById('cancel-upload').addEventListener('click', hideFileUpload);
    
    // Modal close handlers
    document.querySelectorAll('.close, .close-modal').forEach(btn => {
        btn.addEventListener('click', closeModals);
    });
    
    // Click outside modal to close
    document.querySelectorAll('.modal').forEach(modal => {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) closeModals();
        });
    });
}

// ==================== AUTHENTICATION ====================

function showAuthForm(type) {
    document.getElementById('login-tab').classList.toggle('active', type === 'login');
    document.getElementById('register-tab').classList.toggle('active', type === 'register');
    document.getElementById('login-form').style.display = type === 'login' ? 'block' : 'none';
    document.getElementById('register-form').style.display = type === 'register' ? 'block' : 'none';
}

async function handleLogin(e) {
    e.preventDefault();
    
    const email = document.getElementById('login-email').value;
    const password = document.getElementById('login-password').value;
    
    try {
        showLoading(true);
        
        const response = await fetch(`${API_BASE}/auth/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ email, password })
        });
        
        if (response.ok) {
            const data = await response.json();
            authToken = data.token;
            currentUser = { email: data.email };
            
            localStorage.setItem('authToken', authToken);
            localStorage.setItem('currentUser', JSON.stringify(currentUser));
            
            showNotification('Login successful!', 'success');
            showDashboard();
            initializeSignalR();
        } else {
            const error = await response.text();
            showNotification(error || 'Login failed', 'error');
        }
    } catch (error) {
        showNotification('Network error. Please try again.', 'error');
    } finally {
        showLoading(false);
    }
}

async function handleRegister(e) {
    e.preventDefault();
    
    const email = document.getElementById('register-email').value;
    const password = document.getElementById('register-password').value;
    const confirmPassword = document.getElementById('register-confirm').value;
    
    if (password !== confirmPassword) {
        showNotification('Passwords do not match', 'error');
        return;
    }
    
    try {
        showLoading(true);
        
        const response = await fetch(`${API_BASE}/auth/register`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ email, password })
        });
        
        if (response.ok) {
            showNotification('Registration successful! Please login.', 'success');
            showAuthForm('login');
            document.getElementById('register-form-element').reset();
        } else {
            const error = await response.text();
            showNotification(error || 'Registration failed', 'error');
        }
    } catch (error) {
        showNotification('Network error. Please try again.', 'error');
    } finally {
        showLoading(false);
    }
}

function handleLogout() {
    authToken = null;
    currentUser = null;
    localStorage.removeItem('authToken');
    localStorage.removeItem('currentUser');
    
    if (signalRConnection) {
        signalRConnection.stop();
        signalRConnection = null;
    }
    
    showAuthSection();
    showNotification('Logged out successfully', 'success');
}

// ==================== UI NAVIGATION ====================

function showAuthSection() {
    document.getElementById('auth-section').style.display = 'block';
    document.getElementById('dashboard-section').style.display = 'none';
    document.getElementById('current-user').textContent = '';
    document.getElementById('logout-btn').style.display = 'none';
}

function showDashboard() {
    document.getElementById('auth-section').style.display = 'none';
    document.getElementById('dashboard-section').style.display = 'block';
    document.getElementById('current-user').textContent = currentUser.email;
    document.getElementById('logout-btn').style.display = 'block';
    
    // Load initial data
    switchTab('messages');
}

function switchTab(tabName) {
    currentTab = tabName;
    
    // Update tab buttons
    document.querySelectorAll('.nav-tab').forEach(tab => {
        tab.classList.toggle('active', tab.dataset.tab === tabName);
    });
    
    // Update tab content
    document.querySelectorAll('.tab-content').forEach(content => {
        content.classList.toggle('active', content.id === `${tabName}-tab`);
    });
    
    // Load tab-specific data
    switch (tabName) {
        case 'messages':
            loadMessages();
            break;
        case 'groups':
            loadGroups();
            break;
        case 'subscriptions':
            loadSubscriptions();
            break;
        case 'files':
            loadFiles();
            break;
    }
}

// ==================== MESSAGES ====================

async function loadMessages() {
    try {
        const response = await fetch(`${API_BASE}/messages/inbox?page=${currentPage}&pageSize=${PAGE_SIZE}`, {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        if (response.ok) {
            messages = await response.json();
            renderMessages();
        } else {
            showNotification('Failed to load messages', 'error');
        }
    } catch (error) {
        showNotification('Error loading messages', 'error');
    }
}

function renderMessages() {
    const container = document.getElementById('messages-list');
    
    if (!messages || messages.length === 0) {
        container.innerHTML = '<p class="loading">No messages found.</p>';
        return;
    }
    
    container.innerHTML = messages.map(message => `
        <div class="message-item ${!message.isRead ? 'unread' : ''}" data-message-id="${message.id}">
            <div class="message-header">
                <span class="message-sender">${message.senderEmail || `User ${message.senderId}`}</span>
                <span class="message-time">${formatDate(message.sentAt)}</span>
            </div>
            <div class="message-content">${escapeHtml(message.content)}</div>
            ${!message.isRead ? `
                <div class="message-actions">
                    <button class="btn btn-primary" onclick="markAsRead(${message.id})">Mark as Read</button>
                </div>
            ` : ''}
        </div>
    `).join('');
    
    // Update pagination info
    document.getElementById('page-info').textContent = `Page ${currentPage}`;
}

async function handleSendMessage(e) {
    e.preventDefault();
    
    const messageType = document.getElementById('message-type').value;
    const content = document.getElementById('message-content').value;
    const fileInput = document.getElementById('message-file');
    
    let requestData = { content };
    
    if (messageType === 'direct') {
        const recipientId = parseInt(document.getElementById('recipient-id').value);
        if (!recipientId) {
            showNotification('Please enter a recipient ID', 'error');
            return;
        }
        requestData.receiverId = recipientId;
    } else {
        const groupId = parseInt(document.getElementById('group-select').value);
        if (!groupId) {
            showNotification('Please select a group', 'error');
            return;
        }
        requestData.groupId = groupId;
    }
    
    try {
        showLoading(true);
        
        // Handle file upload if present
        if (fileInput.files.length > 0) {
            const formData = new FormData();
            formData.append('file', fileInput.files[0]);
            
            const fileResponse = await fetch('/api/files/upload', {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${authToken}`
                },
                body: formData
            });
            
            if (fileResponse.ok) {
                const fileData = await fileResponse.json();
                requestData.attachmentUrl = fileData.url;
            }
        }
        
        const response = await fetch(`${API_BASE}/messages`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${authToken}`
            },
            body: JSON.stringify(requestData)
        });
        
        if (response.ok) {
            showNotification('Message sent successfully!', 'success');
            closeModals();
            document.getElementById('compose-form').reset();
            loadMessages(); // Refresh messages
        } else {
            const error = await response.text();
            showNotification(error || 'Failed to send message', 'error');
        }
    } catch (error) {
        showNotification('Error sending message', 'error');
    } finally {
        showLoading(false);
    }
}

function handleMessageTypeChange() {
    const messageType = document.getElementById('message-type').value;
    const recipientGroup = document.getElementById('recipient-group');
    const groupSelectGroup = document.getElementById('group-select-group');
    
    if (messageType === 'direct') {
        recipientGroup.style.display = 'block';
        groupSelectGroup.style.display = 'none';
    } else {
        recipientGroup.style.display = 'none';
        groupSelectGroup.style.display = 'block';
        loadGroupsForSelect();
    }
}

async function loadGroupsForSelect() {
    try {
        const response = await fetch(`${API_BASE}/groups`, {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        if (response.ok) {
            const userGroups = await response.json();
            const select = document.getElementById('group-select');
            select.innerHTML = '<option value="">Select a group...</option>' +
                userGroups.map(group => `<option value="${group.id}">${group.name}</option>`).join('');
        }
    } catch (error) {
        console.error('Error loading groups for select:', error);
    }
}

async function markAsRead(messageId) {
    try {
        const response = await fetch(`${API_BASE}/messages/${messageId}/read`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        if (response.ok) {
            showNotification('Message marked as read', 'success');
            loadMessages(); // Refresh messages
        } else {
            showNotification('Failed to mark message as read', 'error');
        }
    } catch (error) {
        showNotification('Error marking message as read', 'error');
    }
}

function changePage(direction) {
    const newPage = currentPage + direction;
    if (newPage >= 1) {
        currentPage = newPage;
        loadMessages();
    }
}

// ==================== GROUPS ====================

async function loadGroups() {
    try {
        const response = await fetch(`${API_BASE}/groups`, {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        if (response.ok) {
            groups = await response.json();
            renderGroups();
        } else {
            showNotification('Failed to load groups', 'error');
        }
    } catch (error) {
        showNotification('Error loading groups', 'error');
    }
}

function renderGroups() {
    const container = document.getElementById('groups-list');
    
    if (!groups || groups.length === 0) {
        container.innerHTML = '<p class="loading">No groups found. Create your first group!</p>';
        return;
    }
    
    container.innerHTML = groups.map(group => `
        <div class="group-card" onclick="showGroupDetails(${group.id})">
            <div class="group-name">${escapeHtml(group.name)}</div>
            <div class="group-description">${escapeHtml(group.description || 'No description')}</div>
            <div class="group-meta">
                <span>Members: ${group.memberCount || 0}</span>
                <span>Created: ${formatDate(group.createdAt)}</span>
            </div>
        </div>
    `).join('');
}

async function handleCreateGroup(e) {
    e.preventDefault();
    
    const name = document.getElementById('group-name').value;
    const description = document.getElementById('group-description').value;
    
    try {
        showLoading(true);
        
        const response = await fetch(`${API_BASE}/groups`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${authToken}`
            },
            body: JSON.stringify({ name, description })
        });
        
        if (response.ok) {
            showNotification('Group created successfully!', 'success');
            closeModals();
            document.getElementById('create-group-form').reset();
            loadGroups(); // Refresh groups
        } else {
            const error = await response.text();
            showNotification(error || 'Failed to create group', 'error');
        }
    } catch (error) {
        showNotification('Error creating group', 'error');
    } finally {
        showLoading(false);
    }
}

async function showGroupDetails(groupId) {
    try {
        const response = await fetch(`${API_BASE}/groups/${groupId}`, {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        if (response.ok) {
            const group = await response.json();
            
            document.getElementById('group-title').textContent = group.name;
            document.getElementById('group-description-text').textContent = group.description || 'No description';
            
            // Render members
            const membersContainer = document.getElementById('members-list');
            if (group.members && group.members.length > 0) {
                membersContainer.innerHTML = group.members.map(member => `
                    <div class="member-item">
                        <div class="member-info">
                            <span>User ${member.userId}</span>
                            <span class="member-role ${member.role.toLowerCase()}">${member.role}</span>
                        </div>
                        ${member.role !== 'Owner' ? `
                            <button class="btn btn-danger" onclick="removeMember(${groupId}, ${member.userId})">Remove</button>
                        ` : ''}
                    </div>
                `).join('');
            } else {
                membersContainer.innerHTML = '<p class="text-muted">No members found.</p>';
            }
            
            // Show group details view
            document.getElementById('groups-list').style.display = 'none';
            document.getElementById('group-details').style.display = 'block';
        } else {
            showNotification('Failed to load group details', 'error');
        }
    } catch (error) {
        showNotification('Error loading group details', 'error');
    }
}

function showGroupsList() {
    document.getElementById('groups-list').style.display = 'grid';
    document.getElementById('group-details').style.display = 'none';
}

async function handleAddMember() {
    const userId = prompt('Enter the user ID to add to this group:');
    if (!userId || isNaN(parseInt(userId))) {
        showNotification('Please enter a valid user ID', 'error');
        return;
    }
    
    const groupId = getCurrentGroupId(); // You'll need to track this
    if (!groupId) {
        showNotification('Error: No group selected', 'error');
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE}/groups/${groupId}/members/${userId}`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        if (response.ok) {
            showNotification('Member added successfully!', 'success');
            showGroupDetails(groupId); // Refresh group details
        } else {
            const error = await response.text();
            showNotification(error || 'Failed to add member', 'error');
        }
    } catch (error) {
        showNotification('Error adding member', 'error');
    }
}

async function removeMember(groupId, userId) {
    if (!confirm('Are you sure you want to remove this member?')) {
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE}/groups/${groupId}/members/${userId}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        if (response.ok) {
            showNotification('Member removed successfully!', 'success');
            showGroupDetails(groupId); // Refresh group details
        } else {
            const error = await response.text();
            showNotification(error || 'Failed to remove member', 'error');
        }
    } catch (error) {
        showNotification('Error removing member', 'error');
    }
}

function getCurrentGroupId() {
    // This is a simple way to track the current group ID
    // In a more complex app, you'd have proper state management
    const groupTitle = document.getElementById('group-title').textContent;
    const group = groups.find(g => g.name === groupTitle);
    return group ? group.id : null;
}

// ==================== SUBSCRIPTIONS ====================

async function loadSubscriptions() {
    try {
        showLoading(true);
        
        // Load subscription summary
        const summaryResponse = await fetch(`${API_BASE}/subscriptions/summary`, {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        // Load available plans
        const plansResponse = await fetch(`${API_BASE}/subscriptions/plans`, {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        if (summaryResponse.ok && plansResponse.ok) {
            const summary = await summaryResponse.json();
            const plans = await plansResponse.json();
            
            renderSubscriptionSummary(summary);
            renderAvailablePlans(plans);
        } else {
            showNotification('Failed to load subscription information', 'error');
        }
    } catch (error) {
        showNotification('Error loading subscriptions', 'error');
    } finally {
        showLoading(false);
    }
}

function renderSubscriptionSummary(summary) {
    const container = document.getElementById('subscription-info');
    
    const allFeatures = ['FileSharing', 'GroupChat', 'PremiumSupport'];
    
    container.innerHTML = allFeatures.map(feature => {
        const activeSubscription = summary.activeSubscriptions.find(s => s.feature === feature);
        const expiredSubscription = summary.expiredSubscriptions.find(s => s.feature === feature);
        
        if (activeSubscription) {
            return `
                <div class="subscription-card active">
                    <div class="feature-name">${feature}</div>
                    <div class="feature-status">✅ Active</div>
                    <div class="feature-dates">
                        Expires: ${formatDate(activeSubscription.endDate)}
                    </div>
                </div>
            `;
        } else if (expiredSubscription) {
            return `
                <div class="subscription-card expired">
                    <div class="feature-name">${feature}</div>
                    <div class="feature-status">❌ Expired</div>
                    <div class="feature-dates">
                        Expired: ${formatDate(expiredSubscription.endDate)}
                    </div>
                </div>
            `;
        } else {
            return `
                <div class="subscription-card">
                    <div class="feature-name">${feature}</div>
                    <div class="feature-status">⚪ Not Subscribed</div>
                    <div class="feature-dates">
                        Available for purchase
                    </div>
                </div>
            `;
        }
    }).join('');
    
    // Add total cost info if there are active subscriptions
    if (summary.activeSubscriptions.length > 0) {
        container.innerHTML += `
            <div class="subscription-card">
                <div class="feature-name">Monthly Cost</div>
                <div class="feature-status">$${summary.monthlyCost.toFixed(2)}</div>
                <div class="feature-dates">
                    Total for all active subscriptions
                </div>
            </div>
        `;
    }
}

function renderAvailablePlans(plans) {
    const container = document.getElementById('plans-list');
    
    if (!plans || plans.length === 0) {
        container.innerHTML = '<p class="loading">No subscription plans available.</p>';
        return;
    }
    
    container.innerHTML = plans.map(plan => `
        <div class="plan-card">
            <div class="plan-feature">${plan.feature}</div>
            <div class="plan-price">$${plan.monthlyPrice}</div>
            <div class="plan-period">per month</div>
            <div class="plan-description">${plan.description || ''}</div>
            <button class="btn btn-primary" onclick="subscribeToPlan('${plan.feature}')">
                Subscribe Now
            </button>
        </div>
    `).join('');
}

async function subscribeToPlan(feature) {
    showNotification('Subscription feature coming soon!', 'warning');
    // In a real app, you'd integrate with a payment processor here
}

// ==================== FILES ====================

function loadFiles() {
    const container = document.getElementById('files-list');
    container.innerHTML = `
        <div class="text-center">
            <h3>File Management</h3>
            <p class="text-muted">File management features are coming soon!</p>
            <p>You can already attach files to messages using the compose message feature.</p>
        </div>
    `;
}

function toggleFileUpload() {
    const form = document.getElementById('file-upload-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
}

function hideFileUpload() {
    document.getElementById('file-upload-form').style.display = 'none';
    document.getElementById('file-upload-form').reset();
}

async function handleFileUpload(e) {
    e.preventDefault();
    
    const fileInput = document.getElementById('file-input');
    const description = document.getElementById('file-description').value;
    
    if (!fileInput.files.length) {
        showNotification('Please select a file to upload', 'error');
        return;
    }
    
    const formData = new FormData();
    formData.append('file', fileInput.files[0]);
    if (description) {
        formData.append('description', description);
    }
    
    try {
        showLoading(true);
        
        const response = await fetch('/api/files/upload', {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${authToken}`
            },
            body: formData
        });
        
        if (response.ok) {
            const result = await response.json();
            showNotification('File uploaded successfully!', 'success');
            hideFileUpload();
        } else {
            const error = await response.text();
            showNotification(error || 'Failed to upload file', 'error');
        }
    } catch (error) {
        showNotification('Error uploading file', 'error');
    } finally {
        showLoading(false);
    }
}

// ==================== SIGNALR REAL-TIME MESSAGING ====================

async function initializeSignalR() {
    if (signalRConnection) {
        return;
    }
    
    try {
        const hubUrl = window.location.hostname === 'localhost' && window.location.port === '5250' 
            ? '/messageHub' 
            : 'http://localhost:5250/messageHub';
            
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => authToken
            })
            .build();
        
        // Handle incoming messages
        signalRConnection.on('ReceiveMessage', (message) => {
            showNotification(`New message from ${message.senderEmail || 'User ' + message.senderId}`, 'success');
            if (currentTab === 'messages') {
                loadMessages(); // Refresh messages if on messages tab
            }
        });
        
        // Handle connection events
        signalRConnection.onclose(() => {
            console.log('SignalR connection closed');
        });
        
        await signalRConnection.start();
        console.log('SignalR connection started');
    } catch (error) {
        console.error('Error starting SignalR connection:', error);
    }
}

// ==================== UTILITY FUNCTIONS ====================

function showModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.style.display = 'flex';
        modal.classList.add('show');
    }
}

function closeModals() {
    document.querySelectorAll('.modal').forEach(modal => {
        modal.style.display = 'none';
        modal.classList.remove('show');
    });
}

function showNotification(message, type = 'info') {
    const container = document.getElementById('notifications');
    const notification = document.createElement('div');
    notification.className = `notification ${type}`;
    notification.innerHTML = `
        <div class="notification-content">
            ${escapeHtml(message)}
        </div>
    `;
    
    container.appendChild(notification);
    
    // Auto remove after 5 seconds
    setTimeout(() => {
        if (notification.parentNode) {
            notification.parentNode.removeChild(notification);
        }
    }, 5000);
    
    // Add click to dismiss
    notification.addEventListener('click', () => {
        if (notification.parentNode) {
            notification.parentNode.removeChild(notification);
        }
    });
}

function showLoading(show) {
    const overlay = document.getElementById('loading-overlay');
    overlay.style.display = show ? 'flex' : 'none';
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString();
}

function escapeHtml(text) {
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.replace(/[&<>"']/g, function(m) { return map[m]; });
}

// Error handling for fetch requests
window.addEventListener('unhandledrejection', event => {
    console.error('Unhandled promise rejection:', event.reason);
    showNotification('An unexpected error occurred', 'error');
});

// Handle network errors
window.addEventListener('online', () => {
    showNotification('Connection restored', 'success');
});

window.addEventListener('offline', () => {
    showNotification('Connection lost', 'warning');
});