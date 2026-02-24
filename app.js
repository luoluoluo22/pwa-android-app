// 灵动传 Pro - IM 核心引擎 (扫码强化版)
let PC_IP = new URLSearchParams(window.location.search).get('ip') || localStorage.getItem('pc_server_ip') || '192.168.1.5';
if (PC_IP) localStorage.setItem('pc_server_ip', PC_IP);

let PC_SERVER_URL = `http://${PC_IP}:3001`;

// DOM 元素
const chatFlow = document.getElementById('chatFlow');
const textInput = document.getElementById('textInput');
const sendBtn = document.getElementById('sendBtn');
const fileInput = document.getElementById('fileInput');
const attachBtn = document.getElementById('attachBtn');
const connectionState = document.getElementById('connection-state');
const statusDot = document.querySelector('.status-dot');
const savedIpEl = document.getElementById('saved-ip');
const scanBtn = document.getElementById('scanBtn');
const readerEl = document.getElementById('reader');

savedIpEl.textContent = PC_IP;

// --- 扫码逻辑 ---
let html5QrCode;
scanBtn.addEventListener('click', () => {
    // 调试检测：库是否加载
    if (typeof Html5Qrcode === 'undefined') {
        alert("错误：扫码组件尚未加载，请检查网络或刷新页面");
        return;
    }

    if (readerEl.style.display === 'none') {
        // 检查相机底层支持
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            alert("抱歉：当前环境不支持直接调用相机。请确保：\n1. 使用 HTTPS 访问\n2. 已授予浏览器相机权限");
            return;
        }

        readerEl.style.display = 'block';
        scanBtn.textContent = '❌ 取消扫码';

        try {
            html5QrCode = new Html5Qrcode("reader");
            html5QrCode.start(
                { facingMode: "environment" },
                { fps: 10, qrbox: { width: 250, height: 250 } },
                (decodedText) => {
                    try {
                        const url = new URL(decodedText);
                        const ip = url.searchParams.get('ip');
                        if (ip) {
                            applyNewIp(ip);
                            stopScan();
                        } else if (/^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$/.test(decodedText)) {
                            applyNewIp(decodedText);
                            stopScan();
                        }
                    } catch (e) {
                        if (/^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$/.test(decodedText)) {
                            applyNewIp(decodedText);
                            stopScan();
                        } else {
                            alert("扫码成功，但内容不符合规约: " + decodedText);
                        }
                    }
                },
                (errorMessage) => { /* 忽略扫描过程报错 */ }
            ).catch(err => {
                alert("无法启动相机：" + err + "\n\n提示：如果是安装的 App，请在手机系统设置->应用->权限中手动开启'相机'。");
                stopScan();
            });
        } catch (e) {
            alert("初始化扫描器失败: " + e.message);
            stopScan();
        }
    } else {
        stopScan();
    }
});

function stopScan() {
    if (html5QrCode) {
        html5QrCode.stop().then(() => {
            readerEl.style.display = 'none';
            scanBtn.textContent = '📷 扫码配对';
        }).catch(() => {
            readerEl.style.display = 'none';
            scanBtn.textContent = '📷 扫码配对';
        });
    }
}

function applyNewIp(ip) {
    PC_IP = ip;
    PC_SERVER_URL = `http://${ip}:3001`;
    localStorage.setItem('pc_server_ip', ip);
    savedIpEl.textContent = ip;
    addMessage({ role: 'system', type: 'text', content: `✅ 配对成功: ${ip}` });
    poll();
}

// 1. 消息记录
let chatHistory = JSON.parse(localStorage.getItem('chat_history') || '[]');

function addMessage(msg) {
    chatHistory.push(msg);
    if (chatHistory.length > 50) chatHistory.shift();
    localStorage.setItem('chat_history', JSON.stringify(chatHistory));
    renderMessage(msg);
}

function renderMessage(msg) {
    const div = document.createElement('div');
    div.className = `bubble ${msg.role === 'me' ? 'sent' : 'received'}`;
    if (msg.role === 'system') div.className = 'system-msg';

    if (msg.type === 'text') {
        div.innerHTML = msg.content + (msg.role === 'ai' ? '<br><small style="color: #10b981; font-size: 10px;">点击拷贝</small>' : '');
        div.onclick = () => copyText(msg.content);
    } else if (msg.type === 'image' || (msg.data && msg.data.startsWith('data:image'))) {
        div.innerHTML = `
            <div class="image-bubble">
                <img src="${msg.url || msg.data}" style="max-width: 100%; border-radius: 8px; display: block;">
                <span class="file-size" style="display:block; font-size:10px; opacity:0.7; margin-top:5px;">图片已接收</span>
            </div>
        `;
    } else {
        div.innerHTML = `
            <div class="file-bubble">
                <span class="file-icon">📄</span>
                <div>
                    <span class="file-name">${msg.name}</span>
                    <span class="file-size">${msg.status}</span>
                </div>
            </div>
        `;
        if (msg.url) div.onclick = () => window.open(msg.url);
    }
    chatFlow.appendChild(div);
    chatFlow.scrollTop = chatFlow.scrollHeight;
}

window.copyText = (text) => {
    navigator.clipboard.writeText(text);
};

// 2. 轮询
async function poll() {
    try {
        const res = await fetch(`${PC_SERVER_URL}/poll`, { mode: 'cors' });
        if (res.ok) {
            statusDot.style.background = '#10b981';
            connectionState.textContent = '电脑助手在线';
            const data = await res.json();
            if (data.hasFile) {
                if (data.type === 'text') {
                    addMessage({ role: 'ai', type: 'text', content: data.content });
                } else {
                    const isImg = data.fileData.includes('image/');
                    if (isImg) {
                        addMessage({ role: 'ai', type: 'image', data: data.fileData, name: data.fileName });
                    } else {
                        const link = document.createElement('a');
                        link.href = data.fileData;
                        link.download = data.fileName;
                        link.click();
                        addMessage({ role: 'ai', type: 'file', name: data.fileName, status: '已接收' });
                    }
                }
            }
        }
    } catch (e) {
        statusDot.style.background = '#ef4444';
        connectionState.textContent = '电脑端离线';
    }
}
setInterval(poll, 3000);

// 3. 发送
textInput.addEventListener('input', () => sendBtn.disabled = !textInput.value.trim());

sendBtn.addEventListener('click', async () => {
    const text = textInput.value.trim();
    if (!text) return;
    try {
        const res = await fetch(`${PC_SERVER_URL}/upload`, {
            method: 'POST',
            headers: { 'Msg-Type': 'text', 'Content-Type': 'text/plain' },
            body: text,
            mode: 'cors'
        });
        if (res.ok) {
            addMessage({ role: 'me', type: 'text', content: text });
            textInput.value = '';
            sendBtn.disabled = true;
        }
    } catch (e) { alert('发送失败，请确认电脑助手已启动'); }
});

attachBtn.addEventListener('click', () => fileInput.click());

fileInput.addEventListener('change', async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    const encodedName = btoa(unescape(encodeURIComponent(file.name)));
    try {
        const res = await fetch(`${PC_SERVER_URL}/upload`, {
            method: 'POST',
            headers: { 'Msg-Type': 'file', 'File-Name': encodedName },
            body: file, mode: 'cors'
        });
        if (res.ok) {
            addMessage({ role: 'me', type: 'file', name: file.name, status: '发送成功' });
            fileInput.value = '';
        }
    } catch (e) { alert('文件投送失败'); }
});

chatHistory.forEach(renderMessage);
poll();
