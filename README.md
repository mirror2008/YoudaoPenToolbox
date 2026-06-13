# 有道词典笔工具箱

**一支笔，尽在掌控。**

连接、管理、探索——  
把词典笔里的一切，收进一个安静而强大的窗口。

---

## 下载

前往 [Releases](https://github.com/mirror2008/YoudaoPenToolbox/releases) 下载最新正式版。

解压后运行 `YoudaoPenToolbox.exe`。  
程序会自动检查更新，新版本发布后将提示下载并重启。

**系统要求：** Windows 10 或更高版本 · .NET Framework 4.8

---

## 功能

### 应用管理

浏览已安装应用，搜索、排序、查看占用。  
安装与卸载，拖拽 AMR 即可部署。  
一键备份为 AMR，一键安装 Loli。  
系统应用受保护，卸载前自动备份。

### 设备洞察

电量、CPU、内存、存储、系统负载——  
实时状态，一目了然。  
多台设备同时连接，左侧列表随时切换。

### miniapp_cli

图形化调用设备端 miniapp_cli。  
参数提示、命令预览、输出解析，  
复杂操作，简单完成。

### 快捷工具

ADB 持久化：解锁一次，重启后授权仍在。  
重启、关机、一键安装 Loli，常用动作触手可及。

### 任务管理器

查看运行进程，终结或重启，  
设备后台，清晰可见。

### 文件管理器

像浏览本地文件夹一样操作设备文件。  
上传、下载、重命名、新建目录，  
文本与二进制文件均可预览。

### ADB 终端

直接执行 ADB 子命令，  
命令预览、输出留存，调试更高效。

### 刷机与分区

列出块分区，提取、批量备份、预设套装备份。  
挂载与卸载，从电脑流式刷写分区。  
高危操作，全程提示，步步可控。

### ADB 解锁

设备未解锁时，引导通过 PenNewInject 完成付费解锁流程，  
下载、解压、启动，一气呵成。

---

## 使用前

1. 用 USB 连接词典笔，开启 USB 调试
2. 若 ADB 未解锁，按程序内提示完成 PenNewInject 解锁
3. 点击「刷新设备」，选择左侧设备即可开始

---

## 构建

```powershell
msbuild YoudaoPenToolbox.sln /p:Configuration=Release
```

输出目录：`YoudaoPenToolbox\bin\Release\`

---

## 开源协议

本项目采用 [AGPL-3.0](LICENSE) 协议。

---

**Powered by MIRROR**

[GitHub](https://github.com/mirror2008/YoudaoPenToolbox)

制作不易，且行且珍惜。
