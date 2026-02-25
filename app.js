// 传输助手 - IM 核心引擎 (Quicker 增强版)
const urlParams = new URLSearchParams(window.location.search)
const urlIp = urlParams.get('ip')
const urlPushKey = urlParams.get('pushKey')

let PC_IP = urlIp || localStorage.getItem('pc_server_ip')
let PUSH_KEY = urlPushKey || localStorage.getItem('quicker_push_key')

const hasNoIp = !PC_IP && !PUSH_KEY

// 微信环境检测
const isWechat = /MicroMessenger/i.test(navigator.userAgent)
if (isWechat) {
  alert(
    '检测到您正在使用微信浏览器。由于微信限制，请点击右上角 [...] 并选择 [在浏览器打开]，否则可能无法正常传输文件。',
  )
}

if (hasNoIp) {
  PC_IP = '192.168.1.5' // 默认 fallback
} else {
  if (PC_IP) localStorage.setItem('pc_server_ip', PC_IP)
  if (PUSH_KEY) localStorage.setItem('quicker_push_key', PUSH_KEY)
}

let PC_SERVER_URL = `http://${PC_IP}:3001`

// DOM 元素
const chatFlow = document.getElementById('chatFlow')
const textInput = document.getElementById('textInput')
const sendBtn = document.getElementById('sendBtn')
const fileInput = document.getElementById('fileInput')
const attachBtn = document.getElementById('attachBtn')
const connectionState = document.getElementById('connection-state')
const statusDot = document.querySelector('.status-dot')
const savedIpEl = document.getElementById('saved-ip')
const scanBtn = document.getElementById('scanBtn')
const readerEl = document.getElementById('reader')
const clearMsgsBtn = document.getElementById('clearMsgs')

// 图片模态框元素
const imageModal = document.getElementById('imageModal')
const imgFull = document.getElementById('imgFull')
const closeBtn = document.querySelector('.close')

if (savedIpEl) savedIpEl.textContent = PC_IP

// --- 消息处理 ---
let chatHistory = JSON.parse(localStorage.getItem('chat_history') || '[]')

function addMessage(msg) {
  chatHistory.push(msg)
  if (chatHistory.length > 50) chatHistory.shift()
  localStorage.setItem('chat_history', JSON.stringify(chatHistory))
  renderMessage(msg)
}

function renderMessage(msg) {
  const div = document.createElement('div')
  div.className = `bubble ${msg.role === 'me' ? 'sent' : 'received'}`
  if (msg.role === 'system') div.className = 'system-msg'

  if (msg.type === 'text') {
    div.innerHTML =
      msg.content +
      (msg.role === 'ai'
        ? '<br><small style="color: #10b981; font-size: 10px;">点击拷贝</small>'
        : '')
    div.onclick = () => copyText(msg.content)
  } else if (
    msg.type === 'image' ||
    (msg.data && msg.data.startsWith('data:image'))
  ) {
    const dataUrl = msg.url || msg.data
    div.innerHTML = `<div class="image-bubble" onclick="zoomImg('${dataUrl}')"><img src="${dataUrl}"><span class="file-size" style="display:block; font-size:10px; opacity:0.7; margin-top:5px;">图片预览</span></div>`
  } else {
    div.innerHTML = `<div class="file-bubble"><span class="file-icon">📄</span><div><span class="file-name">${msg.name}</span><span class="file-size">${msg.status || '文件'}</span></div></div>`
    if (msg.url) div.onclick = () => window.open(msg.url)
  }
  chatFlow.appendChild(div)
  chatFlow.scrollTop = chatFlow.scrollHeight
}

// --- 图片查看 ---
window.zoomImg = (url) => {
  imageModal.style.display = 'block'
  imgFull.src = url
}
if (closeBtn) closeBtn.onclick = () => (imageModal.style.display = 'none')
if (imageModal)
  imageModal.onclick = (e) => {
    if (e.target == imageModal) imageModal.style.display = 'none'
  }

// --- 清空 ---
if (clearMsgsBtn)
  clearMsgsBtn.onclick = () => {
    if (confirm('确定要清空所有聊天记录吗？')) {
      chatHistory = []
      localStorage.removeItem('chat_history')
      chatFlow.innerHTML = '<div class="system-msg">消息已清空</div>'
    }
  }

// --- 扫码 ---
let html5QrCode
if (scanBtn)
  scanBtn.addEventListener('click', () => {
    if (typeof Html5Qrcode === 'undefined') {
      alert('扫码组件加载中...')
      return
    }
    if (readerEl.style.display === 'none') {
      readerEl.style.display = 'block'
      scanBtn.textContent = '❌ 取消'
      html5QrCode = new Html5Qrcode('reader')
      html5QrCode
        .start(
          { facingMode: 'environment' },
          { fps: 10, qrbox: 250 },
          (text) => {
            const url = new URL(text)
            const newIp = url.searchParams.get('ip')
            const newPushKey = url.searchParams.get('pushKey')
            if (newIp) applyNewConfig(newIp, newPushKey)
            stopScan()
          },
        )
        .catch(() => stopScan())
    } else {
      stopScan()
    }
  })

function stopScan() {
  if (html5QrCode)
    html5QrCode.stop().then(() => {
      readerEl.style.display = 'none'
      scanBtn.textContent = '📷 扫码配对'
    })
}

function applyNewConfig(ip, pushKey) {
  PC_IP = ip
  PC_SERVER_URL = `http://${ip}:3001`
  localStorage.setItem('pc_server_ip', ip)
  if (pushKey) {
    PUSH_KEY = pushKey
    localStorage.setItem('quicker_push_key', pushKey)
  }
  if (savedIpEl) savedIpEl.textContent = ip
  addMessage({ role: 'system', type: 'text', content: `✅ 已更新配置` })
  location.reload()
}

// --- 设置交互 ---
function openPushSettings() {
  const key = prompt('请输入 Quicker 推送密钥 (Push Key):', PUSH_KEY || '')
  if (key !== null) {
    localStorage.setItem('quicker_push_key', key)
    location.reload()
  }
}
if (connectionState) connectionState.addEventListener('click', openPushSettings)

// --- 核心交互 ---
let lastMsgId = -1

window.copyText = (text) => {
  if (!text) return
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard
      .writeText(text)
      .then(() => showToast('已复制到剪贴板'))
      .catch(() => fallbackCopyText(text))
  } else {
    fallbackCopyText(text)
  }
}

function fallbackCopyText(text) {
  const textArea = document.createElement('textarea')
  textArea.value = text
  textArea.style.position = 'fixed'
  textArea.style.left = '-9999px'
  document.body.appendChild(textArea)
  textArea.focus()
  textArea.select()
  try {
    document.execCommand('copy')
    showToast('已复制到剪贴板')
  } catch (err) {
    alert('拷贝失败')
  }
  document.body.removeChild(textArea)
}

function showToast(msg) {
  const toast = document.createElement('div')
  toast.className = 'toast-msg'
  toast.textContent = msg
  document.body.appendChild(toast)
  setTimeout(() => toast.classList.add('show'), 10)
  setTimeout(() => {
    toast.classList.remove('show')
    setTimeout(() => document.body.removeChild(toast), 300)
  }, 2000)
}

async function poll() {
  if (!PC_IP) return
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), 5000)

  try {
    const res = await fetch(`${PC_SERVER_URL}/poll?lastId=${lastMsgId}`, {
      mode: 'cors',
      signal: controller.signal,
    })
    clearTimeout(timeoutId)
    if (res.ok) {
      statusDot.style.background = '#10b981'
      connectionState.textContent = `在线: ${PC_IP}`
      const data = await res.json()
      if (data.nextId !== undefined) lastMsgId = data.nextId
      if (data.hasFile) {
        if (data.id !== undefined) lastMsgId = data.id
        if (data.type === 'text') {
          addMessage({ role: 'ai', type: 'text', content: data.content })
        } else {
          handleFileMessage(data)
        }
        setTimeout(poll, 100)
        return
      }
    }
  } catch (e) {
    statusDot.style.background = PUSH_KEY ? '#6366f1' : '#ef4444'
    connectionState.textContent = PUSH_KEY
      ? '推送模式 (局域网离线)'
      : `离线: ${PC_IP}`
  }
  setTimeout(poll, 3000)
}

function handleFileMessage(data) {
  const isImg = data.fileData && data.fileData.includes('image/')
  if (isImg) {
    addMessage({
      role: 'ai',
      type: 'image',
      data: data.fileData,
      name: data.fileName,
    })
  } else {
    const link = document.createElement('a')
    link.href = data.fileData
    link.download = data.fileName
    link.click()
    addMessage({
      role: 'ai',
      type: 'file',
      name: data.fileName,
      status: '已接收',
    })
  }
}

// --- 发送逻辑 ---
if (sendBtn)
  sendBtn.addEventListener('click', async () => {
    const text = textInput.value.trim()
    if (!text) return

    // 1. 尝试 Push API (最稳，但仅限文字)
    if (PUSH_KEY) {
      try {
        const res = await fetch('https://push.getquicker.net/push', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ to: PUSH_KEY, operation: 'copy', data: text }),
        })
        if (res.ok) {
          addMessage({ role: 'me', type: 'text', content: text })
          textInput.value = ''
          sendBtn.disabled = true
          showToast('已通过云端推送')
          return
        }
      } catch (e) {
        console.error('Push API fail', e)
      }
    }

    // 2. 局域网 Fallback
    try {
      const res = await fetch(`${PC_SERVER_URL}/upload`, {
        method: 'POST',
        headers: { 'Msg-Type': 'text', 'Content-Type': 'text/plain' },
        body: text,
        mode: 'cors',
      })
      if (res.ok) {
        addMessage({ role: 'me', type: 'text', content: text })
        textInput.value = ''
        sendBtn.disabled = true
      }
    } catch (e) {
      alert('发送失败，请检查局域网连接')
    }
  })

if (attachBtn) attachBtn.addEventListener('click', () => fileInput.click())
if (fileInput)
  fileInput.addEventListener('change', async (e) => {
    const file = e.target.files[0]
    if (!file) return
    const isImage = file.type.startsWith('image/')
    let localPreviewData = null
    if (isImage) {
      localPreviewData = await new Promise((resolve) => {
        const reader = new FileReader()
        reader.onload = (e) => resolve(e.target.result)
        reader.readAsDataURL(file)
      })
    }
    const encodedName = btoa(unescape(encodeURIComponent(file.name)))
    try {
      const res = await fetch(`${PC_SERVER_URL}/upload`, {
        method: 'POST',
        headers: { 'Msg-Type': 'file', 'File-Name': encodedName },
        body: file,
        mode: 'cors',
      })
      if (res.ok) {
        if (isImage) {
          addMessage({
            role: 'me',
            type: 'image',
            data: localPreviewData,
            name: file.name,
          })
        } else {
          addMessage({
            role: 'me',
            type: 'file',
            name: file.name,
            status: '发送成功',
          })
        }
        fileInput.value = ''
      }
    } catch (e) {
      alert('文件传输仅支持局域网模式，请确保已连接')
    }
  })

// 初始化
chatHistory.forEach(renderMessage)
if (textInput)
  textInput.addEventListener(
    'input',
    () => (sendBtn.disabled = !textInput.value.trim()),
  )
poll()
