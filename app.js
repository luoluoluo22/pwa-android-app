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

// 全局配置：请确保此 IP 与您的电脑局域网 IP 一致
const PC_SERVER_URL = 'http://192.168.1.5:3000';

// 文件传输逻辑
const fileInput = document.getElementById('fileInput');
const fileList = document.getElementById('fileList');
const sendBtn = document.getElementById('sendBtn');
const statusDot = document.querySelector('.status-dot');

// 手机端定时检查电脑端是否有文件传过来 (实现双向)
async function checkForIncomingFiles() {
    try {
        const response = await fetch(`${PC_SERVER_URL}/poll`);
        if (response.ok) {
            const data = await response.json();
            if (data.hasFile) {
                alert(`收到来自电脑的文件: ${data.fileName}`);
                // 触发自动下载
                const link = document.createElement('a');
                link.href = data.fileData;
                link.download = data.fileName;
                link.click();
            }
        }
        statusDot.style.background = '#10b981'; // 保持在线状态
    } catch (e) {
        statusDot.style.background = '#ef4444'; // 连接不到 Quicker
    }
}
// 每 5 秒轮询一次电脑端
setInterval(checkForIncomingFiles, 5000);

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

sendBtn.addEventListener('click', async () => {
    const files = fileInput.files;
    if (files.length > 0) {
        sendBtn.textContent = '正在投送...';
        sendBtn.disabled = true;

        const file = files[0];

        // 使用原生 fetch 发送数据到 C#
        try {
            // 对文件名进行 Base64 编码以处理中文
            const encodedName = btoa(unescape(encodeURIComponent(file.name)));

            const response = await fetch(`${PC_SERVER_URL}/upload`, {
                method: 'POST',
                headers: {
                    'File-Name': encodedName,
                    'Content-Type': 'application/octet-stream'
                },
                body: file
            });

            if (response.ok) {
                sendBtn.textContent = '投送成功！';
                setTimeout(() => {
                    sendBtn.textContent = '发送';
                    sendBtn.disabled = false;
                    fileList.innerHTML = '<div class="empty-hint">等待接收或选择文件...</div>';
                    fileInput.value = '';
                }, 1500);
            }
        } catch (error) {
            alert('投送失败，请检查电脑端 Quicker 服务是否启动');
            sendBtn.textContent = '重试';
            sendBtn.disabled = false;
        }
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
