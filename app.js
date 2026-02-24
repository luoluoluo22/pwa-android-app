// 灵动传 Pro - IM 核心引擎 (v1.1 优化版)
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
const clearMsgsBtn = document.getElementById('clearMsgs');

// 图片模态框元素
const imageModal = document.getElementById('imageModal');
const imgFull = document.getElementById('imgFull');
const closeBtn = document.querySelector('.close');

savedIpEl.textContent = PC_IP;

// --- 消息处理 ---
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
        const dataUrl = msg.url || msg.data;
        div.innerHTML = `
            <div class="image-bubble" onclick="zoomImg('${dataUrl}')">
                <img src="${dataUrl}">
                <span class="file-size" style="display:block; font-size:10px; opacity:0.7; margin:top:5px;">图片预览</span>
            </div>
        `;
    } else {
        div.innerHTML = `
            <div class="file-bubble">
                <span class="file-icon">📄</span>
                <div>
                    <span class="file-name">${msg.name}</span>
                    <span class="file-size">${msg.status || '文件'}</span>
                </div>
            </div>
        `;
        if (msg.url) div.onclick = () => window.open(msg.url);
    }
    chatFlow.appendChild(div);
    chatFlow.scrollTop = chatFlow.scrollHeight;
}

// --- 图片查看 logic ---
window.zoomImg = (url) => {
    imageModal.style.display = "block";
    imgFull.src = url;
};
closeBtn.onclick = () => imageModal.style.display = "none";
imageModal.onclick = (e) => { if (e.target == imageModal) imageModal.style.display = "none"; };

// --- 清空 logic ---
clearMsgsBtn.onclick = () => {
    if (confirm("确定要清空所有聊天记录吗？")) {
        chatHistory = [];
        localStorage.removeItem('chat_history');
        chatFlow.innerHTML = '<div class="system-msg">消息已清空</div>';
    }
};

// --- 扫码 logic ---
let html5QrCode;
scanBtn.addEventListener('click', () => {
    if (typeof Html5Qrcode === 'undefined') { alert("扫码组件加载中..."); return; }
    if (readerEl.style.display === 'none') {
        readerEl.style.display = 'block';
        scanBtn.textContent = '❌ 取消';
        html5QrCode = new Html5Qrcode("reader");
        html5QrCode.start({ facingMode: "environment" }, { fps: 10, qrbox: 250 }, (text) => {
            if (text.includes('ip=')) {
                applyNewIp(new URL(text).searchParams.get('ip'));
                stopScan();
            } else if (/^(?:\d{1,3}\.){3}\d{1,3}$/.test(text)) {
                applyNewIp(text);
                stopScan();
            }
        }).catch(() => stopScan());
    } else { stopScan(); }
});

function stopScan() {
    if (html5QrCode) html5QrCode.stop().then(() => {
        readerEl.style.display = 'none';
        scanBtn.textContent = '📷 扫码配对';
    });
}

function applyNewIp(ip) {
    PC_IP = ip;
    PC_SERVER_URL = `http://${ip}:3001`;
    localStorage.setItem('pc_server_ip', ip);
    savedIpEl.textContent = ip;
    addMessage({ role: 'system', type: 'text', content: `✅ 已连接 IP: ${ip}` });
    poll();
}

// --- 核心交互 ---
window.copyText = (text) => {
    navigator.clipboard.writeText(text);
};

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
                    const isImg = data.fileData && data.fileData.includes('image/');
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

textInput.addEventListener('input', () => sendBtn.disabled = !textInput.value.trim());

sendBtn.addEventListener('click', async () => {
    const text = textInput.value.trim();
    if (!text) return;
    try {
        const res = await fetch(`${PC_SERVER_URL}/upload`, {
            method: 'POST',
            headers: { 'Msg-Type': 'text', 'Content-Type': 'text/plain' },
            body: text, mode: 'cors'
        });
        if (res.ok) {
            addMessage({ role: 'me', type: 'text', content: text });
            textInput.value = '';
            sendBtn.disabled = true;
        }
    } catch (e) { alert('发送失败'); }
});

attachBtn.addEventListener('click', () => fileInput.click());

fileInput.addEventListener('change', async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    const isImage = file.type.startsWith('image/');

    // 如果是图片，先读取用于本地预览
    let localPreviewData = null;
    if (isImage) {
        localPreviewData = await new Promise(resolve => {
            const reader = new FileReader();
            reader.onload = e => resolve(e.target.result);
            reader.readAsDataURL(file);
        });
    }

    const encodedName = btoa(unescape(encodeURIComponent(file.name)));
    try {
        const res = await fetch(`${PC_SERVER_URL}/upload`, {
            method: 'POST',
            headers: { 'Msg-Type': 'file', 'File-Name': encodedName },
            body: file, mode: 'cors'
        });
        if (res.ok) {
            if (isImage) {
                addMessage({ role: 'me', type: 'image', data: localPreviewData, name: file.name });
            } else {
                addMessage({ role: 'me', type: 'file', name: file.name, status: '发送成功' });
            }
            fileInput.value = '';
        }
    } catch (e) { alert('文件发送失败'); }
});

chatHistory.forEach(renderMessage);
poll();
