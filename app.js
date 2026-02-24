let deferredPrompt;
const installBtn = document.getElementById('installBtn');

window.addEventListener('beforeinstallprompt', (e) => {
    // 阻止 Chrome 67 及更早版本自动显示提示
    e.preventDefault();
    // 存储事件以备后用
    deferredPrompt = e;
    // 更新 UI 以通知用户可以安装
    installBtn.style.display = 'block';
});

installBtn.addEventListener('click', async () => {
    if (deferredPrompt) {
        // 显示安装提示
        deferredPrompt.prompt();
        // 等待用户响应
        const { outcome } = await deferredPrompt.userChoice;
        console.log(`User response to the install prompt: ${outcome}`);
        // 事件已使用
        deferredPrompt = null;
        installBtn.style.display = 'none';
    } else {
        alert('请使用移动浏览器（如 Chrome）的菜单中的“添加到主屏幕”功能进行安装。');
    }
});

window.addEventListener('appinstalled', (evt) => {
    console.log('应用已成功安装');
    installBtn.style.display = 'none';
});

// 注册 Service Worker 并引入 Socket.io (通过 index.html)
let socket;
try {
    // 默认尝试连接本地服务器，请在实际使用时替换为您的电脑 IP 地址
    socket = io('http://192.168.1.5:3000');
    socket.on('connect', () => {
        console.log('已连接到后端服务器');
        socket.emit('register', { type: 'Mobile', name: '我的安卓手机' });
        document.querySelector('.status-dot').style.background = '#10b981';
    });
    socket.on('disconnect', () => {
        document.querySelector('.status-dot').style.background = '#ef4444';
    });
} catch (e) {
    console.log('Socket.io 未就绪或服务器未启动');
}

// 文件传输逻辑
const fileInput = document.getElementById('fileInput');
const fileList = document.getElementById('fileList');
const sendBtn = document.getElementById('sendBtn');

fileInput.addEventListener('change', (e) => {
    const files = e.target.files;
    if (files.length > 0) {
        fileList.innerHTML = ''; // 清空提示
        Array.from(files).forEach(file => {
            const item = document.createElement('div');
            item.className = 'file-item';

            const isImage = file.type.startsWith('image/');
            const icon = isImage ? '🖼️' : '📄';

            item.innerHTML = `
                <div class="file-icon">${icon}</div>
                <div class="file-info">
                    <span class="file-name">${file.name}</span>
                    <span class="file-size">${(file.size / 1024).toFixed(1)} KB</span>
                </div>
            `;
            fileList.appendChild(item);
        });
        sendBtn.disabled = false;
    }
});

sendBtn.addEventListener('click', () => {
    const files = fileInput.files;
    if (files.length > 0 && socket) {
        sendBtn.textContent = '传送中...';
        sendBtn.disabled = true;

        const file = files[0]; // 示例仅处理第一个文件
        const reader = new FileReader();

        reader.onload = function (e) {
            const fileData = e.target.result;

            // 使用回调函数确认服务器已收到
            socket.emit('send_file', {
                fileName: file.name,
                fileSize: (file.size / 1024).toFixed(1) + ' KB',
                fileType: file.type,
                fileData: fileData
            }, (response) => {
                if (response && response.status === 'ok') {
                    sendBtn.textContent = '发送成功！';
                    setTimeout(() => {
                        sendBtn.textContent = '发送';
                        sendBtn.disabled = false;
                        fileList.innerHTML = '<div class="empty-hint">等待接收或选择文件...</div>';
                        fileInput.value = '';
                    }, 1500);
                }
            });
        };

        reader.readAsDataURL(file);
    }
});

// 处理 TWA 分享目标 (Share Target)
// 当用户通过安卓分享菜单进入时，处理参数
window.addEventListener('DOMContentLoaded', () => {
    const parsedUrl = new URL(window.location);
    const title = parsedUrl.searchParams.get('title');
    const text = parsedUrl.searchParams.get('text');
    const url = parsedUrl.searchParams.get('url');

    if (title || text || url) {
        fileList.innerHTML = `
            <div class="file-item">
                <div class="file-icon">🔗</div>
                <div class="file-info">
                    <span class="file-name">${title || '分享的内容'}</span>
                    <span class="file-size">${text || url || ''}</span>
                </div>
            </div>
        `;
        sendBtn.disabled = false;
    }
});

// 简单的微交互：标签点击
document.querySelectorAll('.tab-item').forEach(item => {
    item.addEventListener('click', function () {
        document.querySelector('.tab-item.active').classList.remove('active');
        this.classList.add('active');
    });
});
