const express = require('express');
const http = require('http');
const { Server } = require('socket.io');
const cors = require('cors');

const app = express();
app.use(cors());

const server = http.createServer(app);
const io = new Server(server, {
    cors: {
        origin: "*",
        methods: ["GET", "POST"]
    }
});

// 存储在线设备
const devices = new Map();

io.on('connection', (socket) => {
    console.log('新设备连接:', socket.id);

    // 设备注册（手机或电脑）
    socket.on('register', (data) => {
        devices.set(socket.id, {
            id: socket.id,
            type: data.type,
            name: data.name
        });
        console.log(`${data.type} 设备注册: ${data.name}`);
        // 广播当前在线设备列表
        io.emit('device_list', Array.from(devices.values()));
    });

    // 处理文件传输指令
    socket.on('send_file', (data, callback) => {
        console.log(`收到来自 ${socket.id} 的文件传输请求:`, data.fileName);

        // 向所有设备广播文件
        socket.broadcast.emit('receive_file', {
            from: devices.get(socket.id)?.name || '未知设备',
            fileName: data.fileName,
            fileSize: data.fileSize,
            fileType: data.fileType,
            fileData: data.fileData
        });

        // 给发送者一个回执
        if (callback) callback({ status: 'ok' });
    });

    socket.on('disconnect', () => {
        devices.delete(socket.id);
        io.emit('device_list', Array.from(devices.values()));
        console.log('设备断开连接:', socket.id);
    });
});

const PORT = 3000;
server.listen(PORT, () => {
    console.log(`极简传书后端运行在: http://localhost:${PORT}`);
    console.log(`本地局域网请使用您的 IP 地址访问，例如: http://192.168.x.x:${PORT}`);
});
