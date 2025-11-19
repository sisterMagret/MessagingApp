// Application State
let currentUser = null;
let authToken = null;
let currentPage = 1;
let currentTab = 'messages';
let groups = [];
let messages = [];
let signalRConnection = null;
let userSubscriptions = {};

// API Configuration
// Use configuration from config.js (set via environment variable at runtime)
const API_BASE = window.APP_CONFIG?.API_BASE_URL || 'http://34.242.41.55:5250/api';
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
    document.getElementById('add-member-btn').addEventListener('click', showAddMemberModal);
    document.getElementById('delete-group-btn').addEventListener('click', handleDeleteGroup);
    
    // Add member modal events (check if elements exist to avoid errors)
    const searchUserBtn = document.getElementById('search-user-btn');
    const confirmAddMemberBtn = document.getElementById('confirm-add-member');
    if (searchUserBtn) searchUserBtn.addEventListener('click', handleSearchUser);
    if (confirmAddMemberBtn) confirmAddMemberBtn.addEventListener('click', handleConfirmAddMember);
    
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
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            authToken = result.data.token;
            currentUser = { email: result.data.email };
            
            // Debug: Log token info
            console.log('Login successful, token:', authToken ? 'received' : 'missing');
            console.log('Token length:', authToken?.length);
            
            localStorage.setItem('authToken', authToken);
            localStorage.setItem('currentUser', JSON.stringify(currentUser));
            
            showNotification('Login successful!', 'success');
            showDashboard();
            initializeSignalR();
        } else {
            showNotification(result.message || 'Login failed', 'error');
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
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            showNotification(result.message || 'Registration successful! Please login.', 'success');
            showAuthForm('login');
            document.getElementById('register-form-element').reset();
        } else {
            showNotification(result.message || 'Registration failed', 'error');
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
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            messages = result.data?.items || [];
            renderMessages();
        } else {
            showNotification(result.message || 'Failed to load messages', 'error');
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
            // Check if user has File Sharing subscription (feature 2)
            if (!hasFeature(2)) {
                showNotification('File sharing requires a subscription. Please subscribe to the File Sharing plan.', 'warning');
                return;
            }
            
            const formData = new FormData();
            formData.append('file', fileInput.files[0]);
            
            const fileResponse = await fetch(`${API_BASE}/files/upload`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${authToken}`
                    // Note: Don't set Content-Type for FormData - browser sets it automatically with boundary
                },
                body: formData
            });
            
            const fileResult = await fileResponse.json();
            if (fileResponse.ok && fileResult.success) {
                requestData.attachmentUrl = fileResult.data.url;
            } else {
                showNotification(fileResult.message || 'Failed to upload file', 'error');
                return;
            }
        }
        
        // Debug: Check token before sending
        console.log('Sending message with token:', authToken ? 'available' : 'missing');
        console.log('Authorization header:', `Bearer ${authToken}`);
        
        const response = await fetch(`${API_BASE}/messages`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${authToken}`
            },
            body: JSON.stringify(requestData)
        });
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            showNotification(result.message || 'Message sent successfully!', 'success');
            closeModals();
            document.getElementById('compose-form').reset();
            loadMessages(); // Refresh messages
        } else {
            showNotification(result.message || 'Failed to send message', 'error');
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
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            const userGroups = result.data || [];
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
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            showNotification(result.message || 'Message marked as read', 'success');
            loadMessages(); // Refresh messages
        } else {
            showNotification(result.message || 'Failed to mark message as read', 'error');
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
    // Check if user has Group Chat subscription (feature 4)
    if (!hasFeature(4)) {
        const groupsList = document.getElementById('groups-list');
        groupsList.innerHTML = `
            <div class="subscription-required">
                <h3>Group Chat Feature</h3>
                <p>This feature requires a subscription to the Group Chat plan.</p>
                <button class="btn btn-primary" onclick="switchTab('subscriptions')">View Subscription Plans</button>
            </div>
        `;
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE}/groups`, {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            groups = result.data || [];
            renderGroups();
        } else {
            showNotification(result.message || 'Failed to load groups', 'error');
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
    
    // Check if user has Group Chat subscription (feature 4)
    if (!hasFeature(4)) {
        showNotification('Group chat feature requires a subscription. Please subscribe to the Group Chat plan.', 'warning');
        closeModals();
        // Switch to subscriptions tab to show available plans
        switchTab('subscriptions');
        return;
    }
    
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
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            showNotification(result.message || 'Group created successfully!', 'success');
            closeModals();
            document.getElementById('create-group-form').reset();
            loadGroups(); // Refresh groups
        } else {
            showNotification(result.message || 'Failed to create group', 'error');
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
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            const group = result.data;
            
            // Set current group ID for getCurrentGroupId() function
            currentGroupId = groupId;
            
            document.getElementById('group-title').textContent = group.name;
            document.getElementById('group-description-text').textContent = group.description || 'No description';
            
            // Render members
            const membersContainer = document.getElementById('members-list');
            const currentUserId = getCurrentUserId(); // Get current user ID
            const isGroupOwner = group.createdById === currentUserId;
            
            if (group.members && group.members.length > 0) {
                membersContainer.innerHTML = group.members.map(member => {
                    const roleName = getRoleName(member.role);
                    const roleClass = roleName.toLowerCase();
                    console.log(`Member: ${member.userEmail}, Role Number: ${member.role}, Role Name: ${roleName}`);
                    return `
                    <div class="member-item">
                        <div class="member-info">
                            <span>${member.userEmail}</span>
                            <span class="member-role ${roleClass}">${roleName}</span>
                        </div>
                        ${isGroupOwner && member.role !== 3 ? `
                            <button class="btn btn-danger" onclick="removeMember(${groupId}, ${member.userId})">Remove</button>
                        ` : ''}
                    </div>
                    `;
                }).join('');
            } else {
                membersContainer.innerHTML = '<p class="text-muted">No members found.</p>';
            }
            
            // Show/hide management buttons based on ownership
            const addMemberBtn = document.getElementById('add-member-btn');
            const deleteGroupBtn = document.getElementById('delete-group-btn');
            
            if (isGroupOwner) {
                addMemberBtn.style.display = 'inline-block';
                deleteGroupBtn.style.display = 'inline-block';
            } else {
                addMemberBtn.style.display = 'none';
                deleteGroupBtn.style.display = 'none';
            }
            
            // Show group details view
            document.getElementById('groups-list').style.display = 'none';
            document.getElementById('group-details').style.display = 'block';
        } else {
            showNotification(result.message || 'Failed to load group details', 'error');
        }
    } catch (error) {
        showNotification('Error loading group details', 'error');
    }
}

function showGroupsList() {
    document.getElementById('groups-list').style.display = 'grid';
    document.getElementById('group-details').style.display = 'none';
}

function showAddMemberModal() {
    // Check if user has Group Chat subscription (feature 4)
    if (!hasFeature(4)) {
        showNotification('Group chat feature requires a subscription. Please subscribe to the Group Chat plan.', 'warning');
        // Switch to subscriptions tab to show available plans
        switchTab('subscriptions');
        return;
    }
    
    const groupId = getCurrentGroupId();
    if (!groupId) {
        showNotification('Error: No group selected', 'error');
        return;
    }
    
    // Reset modal form
    document.getElementById('member-user-id').value = '';
    document.getElementById('member-email').value = '';
    document.getElementById('user-search-result').style.display = 'none';
    
    showModal('add-member-modal');
}

async function handleSearchUser() {
    const email = document.getElementById('member-email').value.trim();
    if (!email) {
        showNotification('Please enter an email address', 'error');
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE}/auth/search?email=${encodeURIComponent(email)}`, {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            const user = result.data;
            document.getElementById('member-user-id').value = user.userId;
            document.getElementById('found-user-info').textContent = `${user.email} (ID: ${user.userId})`;
            document.getElementById('user-search-result').style.display = 'block';
        } else {
            showNotification(result.message || 'User not found', 'error');
            document.getElementById('user-search-result').style.display = 'none';
        }
    } catch (error) {
        showNotification('Error searching for user', 'error');
    }
}

async function handleConfirmAddMember() {
    const userId = document.getElementById('member-user-id').value.trim();
    if (!userId || isNaN(parseInt(userId))) {
        showNotification('Please enter a valid user ID or search by email first', 'error');
        return;
    }
    
    const groupId = getCurrentGroupId();
    if (!groupId) {
        showNotification('Error: No group selected', 'error');
        return;
    }
    
    try {
        showLoading(true);
        const response = await fetch(`${API_BASE}/groups/${groupId}/members/${userId}`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            showNotification(result.message || 'Member added successfully!', 'success');
            hideModal('add-member-modal');
            showGroupDetails(groupId); // Refresh group details
        } else {
            showNotification(result.message || 'Failed to add member', 'error');
        }
    } catch (error) {
        showNotification('Error adding member', 'error');
    } finally {
        showLoading(false);
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
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            showNotification(result.message || 'Member removed successfully!', 'success');
            showGroupDetails(groupId); // Refresh group details
        } else {
            showNotification(result.message || 'Failed to remove member', 'error');
        }
    } catch (error) {
        showNotification('Error removing member', 'error');
    }
}

async function handleDeleteGroup() {
    const groupId = getCurrentGroupId();
    if (!groupId) {
        showNotification('Error: No group selected', 'error');
        return;
    }

    const confirmDelete = confirm('Are you sure you want to delete this group? This action cannot be undone and will remove all group messages and members.');
    if (!confirmDelete) {
        return;
    }

    try {
        showLoading(true);
        const response = await fetch(`${API_BASE}/groups/${groupId}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });

        const result = await response.json();

        if (response.ok && result.success) {
            showNotification(result.message || 'Group deleted successfully!', 'success');
            showGroupsList(); // Go back to groups list
            loadGroups(); // Refresh groups list
        } else {
            showNotification(result.message || 'Failed to delete group', 'error');
        }
    } catch (error) {
        showNotification('Error deleting group', 'error');
    } finally {
        showLoading(false);
    }
}

// Global variable to track current group ID
let currentGroupId = null;

function getCurrentGroupId() {
    return currentGroupId;
}

function getCurrentUserId() {
    // Parse JWT token to get user ID
    if (!authToken) return null;
    
    try {
        const payload = JSON.parse(atob(authToken.split('.')[1]));
        return parseInt(payload.sub);
    } catch (error) {
        console.error('Error parsing token:', error);
        return null;
    }
}

function getRoleName(roleNumber) {
    switch(roleNumber) {
        case 1: return 'Member';
        case 2: return 'Admin';
        case 3: return 'Owner';
        default: return 'Unknown';
    }
}

// ==================== SUBSCRIPTION FUNCTIONS ====================

function hasFeature(featureNumber) {
    return userSubscriptions && userSubscriptions[featureNumber] === true;
}

function updateUIBasedOnSubscriptions() {
    // File Sharing UI updates (Feature 2)
    const fileAttachmentContainer = document.querySelector('#message-file').closest('div');
    const uploadFileBtn = document.getElementById('upload-file-btn');
    
    if (hasFeature(2)) { // File Sharing feature
        // Show file upload elements
        if (fileAttachmentContainer) {
            fileAttachmentContainer.style.display = 'block';
        }
        if (uploadFileBtn) {
            uploadFileBtn.disabled = false;
            uploadFileBtn.title = 'Upload File';
        }
    } else {
        // Hide/disable file upload elements
        if (fileAttachmentContainer) {
            fileAttachmentContainer.style.display = 'none';
        }
        if (uploadFileBtn) {
            uploadFileBtn.disabled = true;
            uploadFileBtn.title = 'File sharing requires subscription';
        }
    }
    
    // Group Chat UI updates (Feature 4)
    const createGroupBtn = document.getElementById('create-group-btn');
    const groupsTab = document.querySelector('[data-tab="groups"]');
    
    if (hasFeature(4)) { // Group Chat feature
        if (createGroupBtn) {
            createGroupBtn.disabled = false;
            createGroupBtn.title = 'Create Group';
        }
        if (groupsTab) {
            groupsTab.style.opacity = '1';
            groupsTab.title = 'Group Chats';
        }
    } else {
        if (createGroupBtn) {
            createGroupBtn.disabled = true;
            createGroupBtn.title = 'Group chat requires subscription';
        }
        if (groupsTab) {
            groupsTab.style.opacity = '0.5';
            groupsTab.title = 'Group chat requires subscription';
        }
    }
}

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
        
        const summaryResult = await summaryResponse.json();
        const plansResult = await plansResponse.json();
        
        if (summaryResponse.ok && summaryResult.success && plansResponse.ok && plansResult.success) {
            const summary = summaryResult.data;
            const plans = plansResult.data;
            
            // Store subscriptions globally for access control
            // Convert activeSubscriptions array to feature map for hasFeature() checks
            userSubscriptions = {};
            if (summary.activeSubscriptions) {
                summary.activeSubscriptions.forEach(sub => {
                    userSubscriptions[sub.feature] = true;
                });
            }
            
            renderSubscriptionSummary(summary);
            renderAvailablePlans(plans);
            
            // Update UI based on subscription status
            updateUIBasedOnSubscriptions();
        } else {
            showNotification(summaryResult.message || plansResult.message || 'Failed to load subscription information', 'error');
        }
    } catch (error) {
        showNotification('Error loading subscriptions', 'error');
    } finally {
        showLoading(false);
    }
}

function renderSubscriptionSummary(summary) {
    const container = document.getElementById('subscription-info');
    
    // Map feature numbers to names
    const featureNames = {
        1: 'Voice Messages',
        2: 'File Sharing', 
        4: 'Group Chat',
        5: 'Email Alerts'
    };
    
    const allFeatureNumbers = [1, 2, 4, 5];
    
    let summaryHtml = `
        <div class="subscription-overview">
            <h4>Your Subscription Status</h4>
            <p><strong>Monthly Cost:</strong> $${summary.monthlyCost.toFixed(2)}</p>
            <p><strong>Active Features:</strong> ${summary.activeSubscriptions.length}</p>
        </div>
    `;
    
    summaryHtml += allFeatureNumbers.map(featureNumber => {
        const featureName = featureNames[featureNumber];
        const activeSubscription = summary.activeSubscriptions.find(s => s.feature === featureNumber);
        const expiredSubscription = summary.expiredSubscriptions.find(s => s.feature === featureNumber);
        
        if (activeSubscription) {
            return `
                <div class="subscription-card active">
                    <div class="feature-name">${featureName}</div>
                    <div class="feature-status">✅ Active</div>
                    <div class="feature-dates">
                        Expires: ${formatDate(activeSubscription.endDate)}
                    </div>
                </div>
            `;
        } else if (expiredSubscription) {
            return `
                <div class="subscription-card expired">
                    <div class="feature-name">${featureName}</div>
                    <div class="feature-status">❌ Expired</div>
                    <div class="feature-dates">
                        Expired: ${formatDate(expiredSubscription.endDate)}
                    </div>
                </div>
            `;
        } else {
            return `
                <div class="subscription-card">
                    <div class="feature-name">${featureName}</div>
                    <div class="feature-status">⚪ Not Subscribed</div>
                    <div class="feature-dates">
                        Available for purchase
                    </div>
                </div>
            `;
        }
    }).join('');
    
    container.innerHTML = summaryHtml;
}

function renderAvailablePlans(plans) {
    const container = document.getElementById('plans-list');
    
    if (!plans || plans.length === 0) {
        container.innerHTML = '<p class="loading">No subscription plans available.</p>';
        return;
    }
    
    // Map feature numbers to names
    const featureNames = {
        1: 'Voice Messages',
        2: 'File Sharing', 
        4: 'Group Chat',
        5: 'Email Alerts'
    };
    
    container.innerHTML = plans.map(plan => `
        <div class="plan-card">
            <div class="plan-header">
                <div class="plan-feature">${featureNames[plan.feature] || 'Unknown Feature'}</div>
                <div class="plan-price">$${plan.monthlyPrice.toFixed(2)}</div>
                <div class="plan-period">per month</div>
            </div>
            <div class="plan-description">${plan.description || ''}</div>
            <div class="plan-actions">
                <button class="btn btn-primary" onclick="subscribeToPlan(${plan.feature}, '${featureNames[plan.feature]}', ${plan.monthlyPrice})">
                    Subscribe Now
                </button>
            </div>
        </div>
    `).join('');
}

async function subscribeToPlan(featureNumber, featureName, monthlyPrice) {
    if (!confirm(`Subscribe to ${featureName} for $${monthlyPrice.toFixed(2)} per month?`)) {
        return;
    }
    
    try {
        showLoading(true);
        
        // Process payment and subscription in one step
        const paymentResponse = await fetch(`${API_BASE}/payments/purchase`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${authToken}`
            },
            body: JSON.stringify({
                feature: featureNumber,
                months: 1, // Subscribe for 1 month
                paymentToken: 'demo_token_' + Date.now() // In real app, this would come from payment processor
            })
        });
        
        const result = await paymentResponse.json();
        
        if (paymentResponse.ok && result.success) {
            showNotification(result.message || `Successfully subscribed to ${featureName} for $${monthlyPrice.toFixed(2)}!`, 'success');
            
            // Refresh subscription data and update UI
            await loadSubscriptions();
        } else {
            throw new Error(`Payment failed: ${result.message}`);
        }
        
    } catch (error) {
        console.error('Subscription error:', error);
        showNotification('Failed to subscribe. Please try again.', 'error');
    } finally {
        showLoading(false);
    }
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
    // Check if user has File Sharing subscription (feature 2)
    if (!hasFeature(2)) {
        showNotification('File sharing requires a subscription. Please subscribe to the File Sharing plan.', 'warning');
        // Switch to subscriptions tab to show available plans
        switchTab('subscriptions');
        return;
    }
    
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
        
        const response = await fetch(`${API_BASE}/files/upload`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${authToken}`
                // Note: Don't set Content-Type for FormData - browser sets it automatically with boundary
            },
            body: formData
        });
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            showNotification(result.message || 'File uploaded successfully!', 'success');
            hideFileUpload();
        } else {
            showNotification(result.message || 'Failed to upload file', 'error');
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

function hideModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.style.display = 'none';
        modal.classList.remove('show');
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