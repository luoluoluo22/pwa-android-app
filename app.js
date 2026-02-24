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
    if (files.length > 0) {
        sendBtn.textContent = '传送中...';
        sendBtn.disabled = true;

        // 模拟网络延迟
        setTimeout(() => {
            alert(`成功传送 ${files.length} 个文件到电脑端！`);
            sendBtn.textContent = '发送';
            fileList.innerHTML = '<div class="empty-hint">等待接收或选择文件...</div>';
            fileInput.value = '';
        }, 2000);
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
