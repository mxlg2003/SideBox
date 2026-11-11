# Sidebox 官网

Sidebox 官方展示与下载网站。纯净无广告，完全免费。

## 运行

```powershell
cd website
npm install
npm run dev
```

开发地址默认是 `http://127.0.0.1:5173` 或 `http://127.0.0.1:5175`。如果端口已被占用，Vite 会自动递增。

## 构建

```powershell
npm run build
```

构建输出到 `website/dist/`，可直接部署在任意静态托管服务（如 Cloudflare Pages、Vercel、Netlify 或 Nginx）。

## 配置

复制 `.env.example` 为 `.env` 可配置：

- `VITE_DOWNLOAD_URL`：Windows 安装包下载地址（.exe）
