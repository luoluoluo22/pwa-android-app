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

// 简单的微交互：标签点击
document.querySelectorAll('.tab-item').forEach(item => {
    item.addEventListener('click', function () {
        document.querySelector('.tab-item.active').classList.remove('active');
        this.classList.add('active');
    });
});
