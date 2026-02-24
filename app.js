// 极简传书 Pro - 核心核心引擎
const PC_SERVER_URL = 'http://192.168.1.5:3001';

// DOM 元素
const fileInput = document.getElementById('fileInput');
const fileList = document.getElementById('fileList');
const historyList = document.getElementById('historyList');
const sendBtn = document.getElementById('sendBtn');
const statusDot = document.querySelector('.status-dot');
const connectionText = document.getElementById('connection-text');
const dropZone = document.getElementById('dropZone');
const clearHistoryBtn = document.getElementById('clearHistory');

// 1. 初始化传输历史
let transferHistory = JSON.parse(localStorage.getItem('transfer_history') || '[]');

function saveHistory(item) {
    transferHistory.unshift({
        ...item,
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    });
    // 仅保留最近 20 条
    if (transferHistory.length > 20) transferHistory.pop();
    localStorage.setItem('transfer_history', JSON.stringify(transferHistory));
    renderHistory();
}

function renderHistory() {
    historyList.innerHTML = transferHistory.map(item => `
        <div class="history-item">
            <div class="item-info">
                <span class="item-icon">${item.type === 'image' ? '🖼️' : '📄'}</span>
                <div>
                    <span class="item-name">${item.name}</span>
                    <span class="item-time">${item.time}</span>
                </div>
            </div>
            <span class="item-status">${item.status}</span>
        </div>
    `).join('');
}

// 2. 状态监听：轮询电脑端并检查活跃
async function updateStatus() {
    try {
        const res = await fetch(`${PC_SERVER_URL}/poll`, { mode: 'cors' });
        if (res.ok) {
            statusDot.style.background = '#10b981';
            statusDot.style.boxShadow = '0 0 10px #10b981';
            connectionText.textContent = '电脑端已就绪';

            const data = await res.json();
            if (data.hasFile) {
                // 收到电脑端传来的文件
                const link = document.createElement('a');
                link.href = data.fileData;
                link.download = data.fileName;
                link.click();
                saveHistory({ name: data.fileName, type: 'download', status: '已接收 ↓' });
            }
        }
    } catch (e) {
        statusDot.style.background = '#ef4444';
        statusDot.style.boxShadow = '0 0 10px #ef4444';
        connectionText.textContent = '电脑助手未在线';
    }
}
setInterval(updateStatus, 5000);

// 3. 文件处理逻辑
dropZone.addEventListener('click', () => fileInput.click());

fileInput.addEventListener('change', (e) => {
    const file = e.target.files[0];
    if (file) {
        const isImage = file.type.startsWith('image/');
        fileList.innerHTML = `
            <div class="active-preview">
                <div class="upload-icon">${isImage ? '🖼️' : '📄'}</div>
                <p>${file.name}</p>
                <span>${(file.size / 1024).toFixed(1)} KB</span>
            </div>
        `;
        sendBtn.disabled = false;
    }
});

sendBtn.addEventListener('click', async () => {
    const file = fileInput.files[0];
    if (!file) return;

    sendBtn.textContent = '正在投送...';
    sendBtn.disabled = true;

    try {
        const encodedName = btoa(unescape(encodeURIComponent(file.name)));
        const response = await fetch(`${PC_SERVER_URL}/upload`, {
            method: 'POST',
            headers: {
                'File-Name': encodedName,
                'Content-Type': 'application/octet-stream'
            },
            body: file,
            mode: 'cors'
        });

        if (response.ok) {
            sendBtn.textContent = '投送成功！';
            saveHistory({ name: file.name, type: file.type.startsWith('image/') ? 'image' : 'file', status: '已发送 ↑' });

            setTimeout(() => {
                sendBtn.textContent = '投送给电脑';
                fileList.innerHTML = `
                    <div class="empty-hint">
                        <div class="upload-icon">📤</div>
                        <p>继续投送</p>
                    </div>
                `;
                fileInput.value = '';
            }, 1500);
        }
    } catch (error) {
        alert('投送失败，请检查电脑端是否打开了 Quicker 助手');
        sendBtn.textContent = '重试';
        sendBtn.disabled = false;
    }
});

// 4. 清空历史
clearHistoryBtn.addEventListener('click', () => {
    transferHistory = [];
    localStorage.removeItem('transfer_history');
    renderHistory();
});

// 初始化渲染
renderHistory();
updateStatus();

// PWA 安装管理
let deferredPrompt;
window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;
    document.getElementById('installBtn').style.display = 'block';
});

document.getElementById('installBtn').addEventListener('click', async () => {
    if (deferredPrompt) {
        deferredPrompt.prompt();
        const { outcome } = await deferredPrompt.userChoice;
        if (outcome === 'accepted') {
            document.getElementById('installBtn').style.display = 'none';
        }
        deferredPrompt = null;
    }
});
