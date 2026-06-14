# -*- coding: utf-8 -*-
"""Generate YoudaoPenToolbox user manual PDF (Simplified Chinese)."""
import os
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import cm, mm
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
    NextPageTemplate,
    PageBreak,
    PageTemplate,
    Paragraph,
    Preformatted,
    Spacer,
    Table,
    TableStyle,
)
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.cidfonts import UnicodeCIDFont

FONT = "STSong-Light"
VERSION = "1.0.1"
OUTPUT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "docs",
    f"YoudaoPenToolbox_用户手册_v{VERSION}.pdf",
)


def register_fonts():
    pdfmetrics.registerFont(UnicodeCIDFont(FONT))


def build_styles():
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "title",
            fontName=FONT,
            fontSize=28,
            leading=36,
            alignment=TA_CENTER,
            spaceAfter=12,
            textColor=colors.HexColor("#1a1a2e"),
        ),
        "subtitle": ParagraphStyle(
            "subtitle",
            fontName=FONT,
            fontSize=14,
            leading=20,
            alignment=TA_CENTER,
            spaceAfter=6,
            textColor=colors.HexColor("#444"),
        ),
        "h1": ParagraphStyle(
            "h1",
            fontName=FONT,
            fontSize=18,
            leading=24,
            spaceBefore=18,
            spaceAfter=10,
            textColor=colors.HexColor("#16213e"),
        ),
        "h2": ParagraphStyle(
            "h2",
            fontName=FONT,
            fontSize=14,
            leading=20,
            spaceBefore=14,
            spaceAfter=8,
            textColor=colors.HexColor("#0f3460"),
        ),
        "h3": ParagraphStyle(
            "h3",
            fontName=FONT,
            fontSize=12,
            leading=18,
            spaceBefore=10,
            spaceAfter=6,
            textColor=colors.HexColor("#333"),
        ),
        "body": ParagraphStyle(
            "body",
            fontName=FONT,
            fontSize=10.5,
            leading=17,
            alignment=TA_JUSTIFY,
            spaceAfter=6,
        ),
        "bullet": ParagraphStyle(
            "bullet",
            fontName=FONT,
            fontSize=10.5,
            leading=16,
            leftIndent=14,
            spaceAfter=3,
        ),
        "note": ParagraphStyle(
            "note",
            fontName=FONT,
            fontSize=10,
            leading=15,
            leftIndent=10,
            rightIndent=10,
            spaceAfter=8,
            backColor=colors.HexColor("#f5f5f5"),
            borderPadding=6,
        ),
        "toc": ParagraphStyle(
            "toc",
            fontName=FONT,
            fontSize=11,
            leading=20,
            leftIndent=0,
        ),
        "code": ParagraphStyle(
            "code",
            fontName="Courier",
            fontSize=9,
            leading=12,
            leftIndent=12,
            spaceAfter=6,
        ),
        "footer": ParagraphStyle(
            "footer",
            fontName=FONT,
            fontSize=8,
            leading=10,
            alignment=TA_CENTER,
            textColor=colors.grey,
        ),
    }


def header_footer(canvas, doc):
    canvas.saveState()
    w, h = A4
    canvas.setFont(FONT, 8)
    canvas.setFillColor(colors.grey)
    if doc.page > 1:
        canvas.drawString(2 * cm, h - 1.2 * cm, f"有道词典笔工具箱 v{VERSION} 用户手册")
        canvas.drawRightString(w - 2 * cm, h - 1.2 * cm, f"第 {doc.page} 页")
    canvas.drawCentredString(w / 2, 1 * cm, "Powered by MIRROR · https://github.com/mirror2008/YoudaoPenToolbox")
    canvas.restoreState()


def p(text, style="body"):
    return Paragraph(text.replace("\n", "<br/>"), style)


def bullet(text, styles):
    return p(f"• {text}", styles["bullet"])


def note(text, styles):
    return p(f"<b>提示：</b>{text}", styles["note"])


def warn(text, styles):
    return p(f"<b>警告：</b>{text}", styles["note"])


def table(data, col_widths=None):
    t = Table(data, colWidths=col_widths, repeatRows=1)
    t.setStyle(
        TableStyle(
            [
                ("FONT", (0, 0), (-1, -1), FONT, 9),
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#e8eef5")),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.HexColor("#16213e")),
                ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#ccc")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]
        )
    )
    return t


def build_story(styles):
    s = styles
    story = []

    # Cover
    story.append(Spacer(1, 4 * cm))
    story.append(p("有道词典笔工具箱", s["title"]))
    story.append(p("用户手册", s["title"]))
    story.append(Spacer(1, 1 * cm))
    story.append(p(f"版本 v{VERSION}", s["subtitle"]))
    story.append(p("一支笔，尽在掌控。", s["subtitle"]))
    story.append(Spacer(1, 2 * cm))
    story.append(p("Powered by MIRROR", s["subtitle"]))
    story.append(p("https://github.com/mirror2008/YoudaoPenToolbox", s["subtitle"]))
    story.append(PageBreak())

    # TOC
    story.append(p("目录", s["h1"]))
    toc_items = [
        "第一章  产品概述与系统要求",
        "第二章  安装、启动与更新",
        "第三章  主界面与设备连接",
        "第四章  APP 列表（应用管理）",
        "第五章  miniapp_cli 命令中心",
        "第六章  快捷工具",
        "第七章  任务管理器",
        "第八章  文件管理器",
        "第九章  ADB 解锁（PenNewInject）",
        "第十章  ADB 终端",
        "第十一章  刷机与分区操作",
        "第十二章  主题与界面交互",
        "第十三章  自动更新机制",
        "第十四章  常见问题与故障排除",
        "附录 A  miniapp_cli 命令速查表",
        "附录 B  受保护系统应用说明",
        "附录 C  重要路径与文件位置",
        "附录 D  免责声明与安全须知",
    ]
    for item in toc_items:
        story.append(p(item, s["toc"]))
    story.append(PageBreak())

    # Ch1
    story.append(p("第一章  产品概述与系统要求", s["h1"]))
    story.append(
        p(
            "「有道词典笔工具箱」（YoudaoPenToolbox）是一款面向有道词典笔用户的 Windows 桌面管理工具。"
            "通过 USB 连接词典笔，在电脑上完成应用安装卸载、文件浏览、进程管理、ADB 调试、分区备份刷写等操作，"
            "将设备管理集中在一个固定 1440×940 的窗口中，降低命令行使用门槛。",
            s["body"],
        )
    )
    story.append(p("1.1  核心能力一览", s["h2"]))
    caps = [
        "应用管理：浏览、搜索、安装 AMR、卸载、备份、启动小程序，一键安装 Loli",
        "设备洞察：电量、CPU、内存、存储、系统负载实时刷新（约 5 秒）",
        "miniapp_cli：17 条命令图形化执行，含参数提示与输出解析",
        "快捷工具：ADB 持久化、重启/关机、截图、内存调试等",
        "任务管理器：查看进程、终结、重启（受保护进程除外）",
        "文件管理器：上传、下载、重命名、删除、文本/十六进制预览",
        "ADB 终端：执行任意 ADB 子命令，支持交互 Shell",
        "刷机分区：提取、批量备份、挂载、流式刷写块分区",
        "ADB 解锁：引导 PenNewInject 付费解锁流程",
    ]
    for c in caps:
        story.append(bullet(c, s))
    story.append(p("1.2  系统要求", s["h2"]))
    story.append(
        table(
            [
                ["项目", "要求"],
                ["操作系统", "Windows 10 或更高版本"],
                ["运行环境", ".NET Framework 4.8（Windows 10 通常已预装）"],
                ["硬件", "USB 接口；建议预留至少 500 MB 磁盘空间（含备份）"],
                ["设备", "有道词典笔，已开启 USB 调试"],
                ["网络", "部分功能需联网（更新、Loli 安装、PenNewInject 下载）"],
            ],
            [4 * cm, 12 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("1.3  分发形式（v1.0.1 起）", s["h2"]))
    story.append(
        p(
            "发布包为<strong>单个 YoudaoPenToolbox.exe</strong>，HandyControl、Newtonsoft.Json 等依赖已嵌入程序内部。"
            "ADB 运行组件（adb.exe、AdbWinApi.dll、AdbWinUsbApi.dll）同样内置于 exe，"
            "<strong>首次启动时自动释放到 exe 同目录</strong>，无需单独下载或手动配置。"
            "释放完成后，同目录会出现上述三个文件，属正常现象。",
            s["body"],
        )
    )
    story.append(PageBreak())

    # Ch2
    story.append(p("第二章  安装、启动与更新", s["h1"]))
    story.append(p("2.1  获取与安装", s["h2"]))
    story.append(
        p(
            "从 GitHub Releases（https://github.com/mirror2008/YoudaoPenToolbox/releases）下载最新 zip，"
            "解压后得到 YoudaoPenToolbox.exe。可将 exe 放在任意文件夹，建议路径不含特殊字符。"
            "无需安装程序，双击即可运行。",
            s["body"],
        )
    )
    story.append(p("2.2  首次启动流程", s["h2"]))
    steps = [
        "程序初始化主题设置（读取 %AppData%\\YoudaoPenToolbox\\theme.txt）",
        "检查远程版本（Gitee），若有强制更新则弹出更新窗口，完成后自动重启",
        "显示启动页（Splash）：Windows 风格加载动画",
        "释放内置 ADB 组件（若同目录缺失）：状态显示「正在释放 adb.exe (1/3)...」等",
        "加载主窗口，后台扫描 ADB 设备，启动页碎裂退场动画后进入主界面",
    ]
    for i, step in enumerate(steps, 1):
        story.append(bullet(f"{i}. {step}", s))
    story.append(
        note(
            "启动页最少显示约 3 秒，确保动画完整播放。若释放组件失败，会弹窗提示并退出程序。",
            s,
        )
    )
    story.append(p("2.3  启动失败排查", s["h2"]))
    story.append(
        table(
            [
                ["现象", "可能原因", "解决方法"],
                [
                    "无法准备必要运行组件",
                    "exe 目录无写权限；磁盘已满",
                    "换到有写权限的目录；清理磁盘空间",
                ],
                [
                    "程序发生错误 + error.log",
                    "未处理异常",
                    "查看 exe 同目录 error.log 详情",
                ],
                [
                    "双击无反应",
                    "缺少 .NET 4.8",
                    "安装 .NET Framework 4.8 运行库",
                ],
            ],
            [3.5 * cm, 4.5 * cm, 8 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("2.4  使用前三步", s["h2"]))
    for step in [
        "用 USB 数据线连接词典笔，在设备上开启 USB 调试",
        "若 ADB 尚未解锁，按程序提示完成 PenNewInject 付费解锁（见第九章）",
        "点击顶部「刷新设备」，在左侧列表选择你的设备",
    ]:
        story.append(bullet(step, s))
    story.append(PageBreak())

    # Ch3
    story.append(p("第三章  主界面与设备连接", s["h1"]))
    story.append(p("3.1  顶部工具栏", s["h2"]))
    story.append(
        table(
            [
                ["控件", "功能说明"],
                ["标题 + 版本号", "显示当前程序版本（如 v1.0.1）"],
                ["主题切换", "跟随时间 / 浅色 / 深色 三种模式"],
                ["adb.exe 路径", "可手动输入或点击「浏览」指定自定义 adb 路径"],
                ["刷新设备", "执行 adb devices -l 扫描已连接设备"],
            ],
            [4 * cm, 12 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("3.2  左侧设备列表", s["h2"]))
    story.append(
        p(
            "列表显示所有 state=device 的在线设备，包含显示名、序列号、连接状态（device 为绿色，offline 为红色）。"
            "支持<strong>多台设备同时连接</strong>，点击列表项切换当前操作对象。"
            "程序每 5 秒后台轮询设备状态，断开会自动移出列表并 Growl 警告，重连同序列号设备会自动重新选中。",
            s["body"],
        )
    )
    story.append(p("3.3  设备信息区", s["h2"]))
    story.append(
        p(
            "选中设备后显示：设备名、品牌 · Android 版本 · 平台、主机名。"
            "下方横向滚动卡片展示：电量、CPU 使用率、运行内存、系统负载（1/5/15 分钟）、/userdisk 存储占用。"
            "数据每 5 秒自动刷新。",
            s["body"],
        )
    )
    story.append(p("3.4  ADB 未解锁时的表现", s["h2"]))
    story.append(
        p(
            "若 Shell 探测失败（echo toolbox_probe_ok 无正确返回），顶部出现橙色横幅："
            "「当前设备尚未解锁 ADB…」，并提供「付费解锁」按钮。"
            "此时应用列表、文件管理、分区等功能不可用，需先完成第九章的解锁流程。",
            s["body"],
        )
    )
    story.append(p("3.5  底部状态栏", s["h2"]))
    story.append(
        p(
            "左侧显示当前操作状态（如「就绪」「正在加载应用列表」「安装成功」等）；"
            "执行耗时操作时右侧显示不确定进度条。",
            s["body"],
        )
    )
    story.append(p("3.6  九个主标签页", s["h2"]))
    story.append(
        table(
            [
                ["标签", "用途"],
                ["APP 列表", "应用浏览、安装、卸载、备份"],
                ["miniapp_cli", "图形化 CLI 命令执行"],
                ["快捷工具", "ADB 持久化、重启、截图等"],
                ["任务管理器", "进程查看与管控"],
                ["文件管理器", "设备文件浏览与操作"],
                ["ADB破解", "ADB 解锁说明与入口"],
                ["ADB 终端", "ADB 子命令执行"],
                ["刷机", "分区提取、挂载、刷写"],
                ["关于", "版本信息与 GitHub 链接"],
            ],
            [3.5 * cm, 12.5 * cm],
        )
    )
    story.append(PageBreak())

    # Ch4 APP
    story.append(p("第四章  APP 列表（应用管理）", s["h1"]))
    story.append(p("4.1  界面布局", s["h2"]))
    story.append(
        p(
            "顶部工具栏含搜索框与操作按钮；中部 DataGrid 固定显示 4 行应用列表，支持按「占用大小」列排序；"
            "下方为选中应用详情面板与 AMR 拖放安装区。",
            s["body"],
        )
    )
    story.append(p("4.2  工具栏按钮", s["h2"]))
    story.append(
        table(
            [
                ["按钮", "说明"],
                ["搜索", "按名称、AppId、分类过滤（不区分大小写）"],
                ["刷新列表", "从设备 packages.json 重新加载应用及占用"],
                ["刷新占用", "仅重新计算各应用占用大小，不重新读列表"],
                ["备份 AMR", "将选中应用从安装目录反向打包为 .amr 文件"],
                ["一键安装 Loli", "自动识别 RK/CVI 芯片，从 Gitee 下载最新 Loli 并安装"],
                ["显示系统应用", "勾选后显示系统内置应用；关闭时仅显示第三方/可卸载应用"],
                ["启动选中", "按下方「启动页面」输入启动小程序"],
                ["卸载选中", "卸载当前选中应用（受保护应用有特殊流程）"],
            ],
            [3.5 * cm, 12.5 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("4.3  安装 AMR（拖放）", s["h2"]))
    for step in [
        "将 .amr 文件拖入底部拖放区（仅支持 .amr 扩展名）",
        "弹出安装确认对话框，显示应用名、版本、AppId、大小、图标、目标设备",
        "确认后上传到 /userdisk/ypt_install_{AppId}_{时间戳}.amr",
        "执行 miniapp_cli install，成功后自动刷新应用列表",
    ]:
        story.append(bullet(step, s))
    story.append(note("拖入后<strong>不会自动安装</strong>，必须确认对话框点确定。", s))
    story.append(p("4.4  卸载应用", s["h2"]))
    story.append(p("<b>普通第三方应用：</b>", s["body"]))
    for step in [
        "询问：卸载前是否备份 AMR？（是 / 否 / 取消）",
        "若选「是」，弹出保存对话框选择备份路径",
        "最终确认后执行 miniapp_cli uninstall",
        "若已备份，卸载完成后提示「AMR 应用找回」说明",
    ]:
        story.append(bullet(step, s))
    story.append(p("<b>受保护系统应用：</b>见附录 B，卸载前强制自动备份到文档目录。", s["body"]))
    story.append(p("4.5  备份 AMR", s["h2"]))
    story.append(
        p(
            "选中应用后点击「备份 AMR」，程序在设备端 zip 打包安装目录，再 pull 到电脑。"
            "系统/内置应用备份前会警告「仅供存档，通常禁止重复安装」。"
            "成功后弹窗显示文件数、大小、保存路径。",
            s["body"],
        )
    )
    story.append(p("4.6  一键安装 Loli", s["h2"]))
    for step in [
        "识别芯片：RK 平台（检测 GStreamer）或 CVI 平台（检测 FFmpeg，含 a6-a7 / x5-s6 / x5-s6-s7 子路径）",
        "从 Gitee youdao-pen-loli 查询最新 loli_v*.amr",
        "确认对话框显示设备、平台、版本、文件名",
        "下载到 %TEMP%\\YoudaoPenToolbox\\loli\\ 后走 AMR 安装流程",
    ]:
        story.append(bullet(step, s))
    story.append(
        note("若无法识别芯片，Growl 提示「未能自动识别设备芯片类型」，可手动拖入 AMR 安装。", s)
    )
    story.append(p("4.7  启动应用", s["h2"]))
    story.append(
        p(
            "在详情区「启动页面」输入框填写页面路由（默认 index），点击「启动选中」。"
            "等价命令：miniapp_cli start {AppId} --{page}（page 不需写 -- 前缀）。",
            s["body"],
        )
    )
    story.append(PageBreak())

    # Ch5 miniapp_cli
    story.append(p("第五章  miniapp_cli 命令中心", s["h1"]))
    story.append(
        p(
            "miniapp_cli 是设备端小程序管理命令行工具。本工具箱将其图形化："
            "下拉选择命令、填写参数、预览完整 shell 命令、执行并解析输出。",
            s["body"],
        )
    )
    story.append(p("5.1  界面说明", s["h2"]))
    story.append(
        table(
            [
                ["区域", "说明"],
                ["命令下拉框", "格式 [分类] 说明，共 17 条命令"],
                ["填入选中应用", "将 APP 列表选中项 AppId 填入参数 1；start 命令时参数 2 填启动页"],
                ["命令说明", "用法、参数提示、示例、Notes，支持滚动"],
                ["即将执行", "Consolas 字体预览完整命令"],
                ["参数输入", "单参数或双参数自适应布局"],
                ["输出日志", "易读解析结果 + 原始输出，带时间戳"],
                ["清空输出", "清除 CommandOutput 内容"],
            ],
            [3.5 * cm, 12.5 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("5.2  命令分类概览", s["h2"]))
    story.append(p("内存调试（7 条）", s["h3"]))
    story.append(
        p(
            "memoryApp、trimImageCache、debugApp、debugService、memoryUsage、memoryUsageGC、dumpMemory。"
            "用于排查内存问题；memoryUsage 需先用 debugApp 指定目标应用。",
            s["body"],
        )
    )
    story.append(p("应用管理（5 条）", s["h3"]))
    story.append(p("install、uninstall、start、startService。安装需设备上已有 AMR 绝对路径。", s["body"]))
    story.append(p("屏幕工具（2 条）", s["h3"]))
    story.append(p("capture、captureFB。截图保存到设备路径，可用 adb pull 或快捷工具拉取到电脑。", s["body"]))
    story.append(p("测试（2 条）", s["h3"]))
    story.append(warn("beginMonkey / stopMonkey：随机压力测试，谨慎使用，可能导致应用异常。", s))
    story.append(p("输入模拟 / 系统配置", s["h3"]))
    story.append(p("injectKey（如 3=Home, 4=Back, 82=Menu）；setRenderConfig key value。", s["body"]))
    story.append(p("5.3  常用 KeyCode 参考", s["h2"]))
    story.append(
        table(
            [
                ["KeyCode", "含义"],
                ["3", "Home 键"],
                ["4", "Back 键"],
                ["82", "Menu 键"],
                ["24", "音量 +"],
                ["25", "音量 -"],
                ["26", "电源键"],
            ],
            [4 * cm, 12 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(note("完整命令参数见附录 A 速查表。", s))
    story.append(PageBreak())

    # Ch6 Quick tools
    story.append(p("第六章  快捷工具", s["h1"]))
    story.append(p("6.1  ADB 持久化", s["h2"]))
    story.append(
        p(
            "词典笔重启后 ADB 授权可能失效。ADB 持久化通过在设备开机脚本中写入钩子，"
            "自动创建授权标记文件 /tmp/.adb_auth_verified，使重启后无需在 PC 上重复 adb shell auth。",
            s["body"],
        )
    )
    story.append(
        table(
            [
                ["按钮", "作用"],
                ["刷新状态", "检测 Shell、授权文件、skip_re 钩子状态"],
                ["启用持久化", "部署脚本 + 写入 skip_login.sh 开机钩子 + 立即授权"],
                ["关闭持久化", "移除钩子与脚本（保留当前授权文件）"],
                ["立即生效", "仅当前开机创建 /tmp/.adb_auth_verified"],
                ["测试钩子", "删除授权文件后模拟 skip_login.sh 验证钩子是否生效"],
            ],
            [3.5 * cm, 12.5 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(
        table(
            [
                ["状态摘要", "含义"],
                ["未配置", "钩子未安装"],
                ["已配置", "钩子已装 + 授权文件存在"],
                ["已配置（重启后自动授权）", "钩子已装，授权文件暂不存在（重启后应自动创建）"],
                ["请先解锁 ADB 再来", "Shell 不可用，需先完成 PenNewInject"],
            ],
            [5 * cm, 11 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(
        note(
            "启用前建议先在 PC 执行 adb shell auth（输入密码）成功，再点「启用持久化」。"
            "启用后建议重启设备验证。诊断信息会写入 miniapp_cli 输出区。",
            s,
        )
    )
    story.append(p("6.2  设备控制", s["h2"]))
    story.append(
        table(
            [
                ["按钮", "底层命令", "确认提示"],
                ["重启设备", "sync; reboot", "重启后 ADB 连接会断开"],
                ["关闭设备", "sync; poweroff", "关机后需手动开机才能重新连接"],
            ],
            [3 * cm, 5 * cm, 8 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("6.3  其他快捷按钮", s["h2"]))
    story.append(
        table(
            [
                ["按钮", "命令/行为"],
                ["一键安装 Loli", "同 APP 列表"],
                ["进程内存 memoryApp", "miniapp_cli memoryApp"],
                ["清理图片缓存", "miniapp_cli trimImageCache"],
                ["QuickJS 内存", "miniapp_cli memoryUsage"],
                ["GC 后内存", "miniapp_cli memoryUsageGC"],
                ["屏幕截图 capture", "截图到 /tmp 再 pull 到电脑"],
                ["Framebuffer 截图", "captureFB + pull PNG"],
                ["导出内存快照", "dumpMemory，输出 /tmp/httpdump.snapshot，需确认"],
            ],
            [4.5 * cm, 11.5 * cm],
        )
    )
    story.append(PageBreak())

    # Ch7 Task manager
    story.append(p("第七章  任务管理器", s["h1"]))
    story.append(
        p(
            "基于 top -b -n 1 解析进程列表，按内存占用排序，每 5 秒自动刷新。"
            "表格列：PID、用户、CPU%、内存%、虚拟内存、状态、命令、路径。",
            s["body"],
        )
    )
    story.append(p("7.1  进程操作", s["h2"]))
    story.append(
        table(
            [
                ["操作", "说明"],
                ["终结进程", "先 SIGTERM (kill -15)，400ms 后仍存活则 SIGKILL (kill -9)，需确认"],
                ["重启进程", "读取 /proc/PID/cmdline → kill → nohup 重启，需确认"],
            ],
            [3 * cm, 13 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("7.2  受保护进程", s["h2"]))
    story.append(
        p(
            "PID ≤ 1（init）及命令行含 adbd 的进程不可终结或重启，按钮禁用并显示原因。"
            "请勿尝试终止 adbd，否则 ADB 连接会断开。",
            s["body"],
        )
    )
    story.append(PageBreak())

    # Ch8 File browser
    story.append(p("第八章  文件管理器", s["h1"]))
    story.append(p("8.1  导航", s["h2"]))
    story.append(
        table(
            [
                ["操作", "说明"],
                ["路径栏 + 前往", "跳转到指定设备路径（Enter 等同前往）"],
                ["返回上一层", "上级目录；根目录 / 时禁用"],
                ["刷新", "重新列出当前目录"],
                ["进入", "进入选中文件夹或链接目录"],
                ["双击文件夹", "同「进入」"],
                ["双击文件", "弹出操作对话框（下载/记事本/十六进制）"],
            ],
            [3.5 * cm, 12.5 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("8.2  文件操作", s["h2"]))
    story.append(
        table(
            [
                ["操作", "说明"],
                ["新建文件夹", "TextInputDialog 输入名称（不可含 / \\ . ..）"],
                ["重命名", "单选时可用，mv 命令"],
                ["删除", "支持 Ctrl/Shift 多选批量删除，不可撤销，需确认"],
                ["上传文件", "系统文件选择器，支持多选"],
                ["拖放上传", "拖文件到拖放区，上传到当前目录"],
                ["下载到电脑", "adb pull + SaveFileDialog"],
                ["记事本打开", "内置文本查看器，最大预览 2 MB"],
                ["二进制查看", "十六进制 + ASCII，最大 2 MB"],
            ],
            [3.5 * cm, 12.5 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("8.3  多选快捷键", s["h2"]))
    story.append(bullet("Ctrl + 点击：多选", s))
    story.append(bullet("Shift + 点击：范围选择", s))
    story.append(bullet("Ctrl + A：全选当前列表", s))
    story.append(
        warn("操作系统分区（如 /system）可能只读，删除或上传会 Permission denied。", s)
    )
    story.append(PageBreak())

    # Ch9 ADB unlock
    story.append(p("第九章  ADB 解锁（PenNewInject）", s["h1"]))
    story.append(
        p(
            "词典笔出厂时 ADB Shell 可能未开放。需通过 PenNewInject 工具完成付费解锁后，"
            "工具箱才能执行 Shell、文件浏览、应用管理等全部功能。",
            s["body"],
        )
    )
    story.append(p("9.1  入口", s["h2"]))
    story.append(bullet("设备信息区橙色横幅「付费解锁」按钮", s))
    story.append(bullet("「ADB破解」标签页", s))
    story.append(p("9.2  解锁向导三步", s["h2"]))
    story.append(p("<b>第一步 介绍页</b>：说明需通过 PenNewInject 付费解锁。", s["body"]))
    story.append(p("<b>第二步 付费说明</b>：", s["body"]))
    story.append(bullet("联系邮箱 nullplex2000@proton.me 获取 KEY", s))
    story.append(bullet("付费金额 25 元", s))
    story.append(p("<b>第三步 下载进度</b>：", s["body"]))
    for step in [
        "从 Gitee 下载 PenNewInject 分片包（manifest: penmirror/ADB/daxiao）",
        "按需下载 7-Zip 组件（7z.exe、7z.dll）并解压",
        "启动 PenNewInject.Pro.exe，按工具内指引输入 KEY 完成解锁",
        "下载过程中对话框不可关闭；失败可重试",
    ]:
        story.append(bullet(step, s))
    story.append(note("缓存目录：%TEMP%\\YoudaoPenToolbox\\PenNewInject\\", s))
    story.append(p("9.3  解锁后", s["h2"]))
    story.append(
        p(
            "重新选择设备，Shell 探测通过后横幅消失，全部功能可用。"
            "「ADB破解」页显示「当前设备 ADB 已可用」。",
            s["body"],
        )
    )
    story.append(PageBreak())

    # Ch10 ADB terminal
    story.append(p("第十章  ADB 终端", s["h1"]))
    story.append(
        p(
            "直接执行 ADB 子命令，自动附加 -s 当前设备序列号。"
            "输入框示例：shell ls /userdisk、pull /path/local、shell cat /etc/os-release。"
            "Enter 或「执行」按钮运行；输出带 [HH:mm:ss] 时间戳，最新在上。",
            s["body"],
        )
    )
    story.append(
        table(
            [
                ["按钮", "说明"],
                ["重启设备 / 关闭设备", "同快捷工具"],
                ["打开交互 Shell", "新控制台窗口 adb shell，可交互输入"],
                ["清空输出", "清除终端输出区"],
            ],
            [4 * cm, 12 * cm],
        )
    )
    story.append(
        note(
            "可输入完整 adb 命令或省略 adb 前缀的子命令。"
            "命令预览区显示：adb -s &lt;序列号&gt; &lt;子命令&gt;",
            s,
        )
    )
    story.append(PageBreak())

    # Ch11 Partition
    story.append(p("第十一章  刷机与分区操作", s["h1"]))
    story.append(
        warn(
            "分区提取与刷写属于<strong>高危操作</strong>，操作不当可能导致设备无法启动（变砖）。"
            "请充分了解风险后再操作，重要分区务必先备份。",
            s,
        )
    )
    story.append(p("11.1  分区列表", s["h2"]))
    story.append(
        p(
            "切换到「刷机」标签时自动加载分区表（若未加载）。"
            "摘要显示 A/B 槽、分区总数、命名分区数、已挂载数、整盘大小。"
            "表格列：备份勾选、分区名、大小、A/B 槽、挂载状态、底层设备、块设备、by-name 路径、风险等级。",
            s["body"],
        )
    )
    story.append(p("11.2  风险等级", s["h2"]))
    story.append(
        p(
            "名称含 uboot、boot、trust、system、recovery，或等于 misc 的分区标记为「高」风险。"
            "已挂载分区提取可能数据不一致；刷写已挂载分区风险更高。",
            s["body"],
        )
    )
    story.append(p("11.3  操作说明", s["h2"]))
    story.append(
        table(
            [
                ["操作", "说明", "风险"],
                ["提取到电脑", "单分区 dd 流式提取到 img 文件", "低（只读）"],
                ["批量提取选中", "勾选「备份」列的多个分区", "低"],
                ["一键备份常用套装", "boot + system + trust + userdata（优先当前 A/B 槽）", "低"],
                ["挂载分区", "输入挂载点；关键分区默认只读 (ro)", "中"],
                ["卸载分区", "卸载已挂载分区", "中"],
                ["从电脑刷入", "dd 流式写入；镜像不得超过分区容量", "高"],
                ["取消传输", "中断进行中的 dd 流", "-"],
            ],
            [3.5 * cm, 7.5 * cm, 2 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(p("11.4  传输机制与保存路径", s["h2"]))
    story.append(
        p(
            "采用 adb shell dd 在电脑与块设备间<strong>直接流式传输</strong>，"
            "不经过 /tmp 或 /userdisk 暂存，节省设备存储空间。",
            s["body"],
        )
    )
    story.append(bullet("默认备份目录：文档\\YoudaoPenToolbox\\PartitionBackups\\{设备序列号}\\", s))
    story.append(bullet("单分区文件名：{分区名}_{时间戳}.img", s))
    story.append(bullet("批量备份子目录：batch_{时间戳}\\", s))
    story.append(note("整盘分区 mmcblk0 不可挂载。", s))
    story.append(PageBreak())

    # Ch12 Theme
    story.append(p("第十二章  主题与界面交互", s["h1"]))
    story.append(p("12.1  主题模式", s["h2"]))
    story.append(
        table(
            [
                ["模式", "行为"],
                ["跟随时间", "06:00–17:59 浅色；18:00–05:59 深色；每分钟检查"],
                ["浅色", "HandyControl SkinDefault"],
                ["深色", "HandyControl SkinDark"],
            ],
            [3.5 * cm, 12.5 * cm],
        )
    )
    story.append(note("主题偏好保存在 %AppData%\\YoudaoPenToolbox\\theme.txt", s))
    story.append(p("12.2  对话框体系", s["h2"]))
    story.append(
        table(
            [
                ["对话框", "用途"],
                ["AppMessageBox", "替代系统 MessageBox，支持 OK/Yes/No/Cancel，长文本滚动，丝滑动画"],
                ["InstallConfirmDialog", "AMR 安装确认（图标、元数据）"],
                ["PenNewInjectUnlockDialog", "ADB 解锁三步向导"],
                ["UpdateWindow", "强制更新（不可跳过主界面）"],
                ["TextInputDialog", "新建文件夹、重命名、挂载点输入"],
                ["RemoteFileActionDialog", "文件：下载 / 记事本 / 十六进制"],
                ["RemoteFileViewerWindow", "大文件虚拟化查看"],
                ["SplashWindow", "启动页 + 碎裂退场动画"],
            ],
            [4.5 * cm, 11.5 * cm],
        )
    )
    story.append(p("12.3  Growl 通知", s["h2"]))
    story.append(
        p(
            "HandyControl 右上角 Toast 通知，用于非阻塞反馈：设备断开/重连、安装卸载结果、"
            "文件操作结果、分区操作结果、Loli 安装、持久化状态等。",
            s["body"],
        )
    )
    story.append(PageBreak())

    # Ch13 Update
    story.append(p("第十三章  自动更新机制", s["h1"]))
    story.append(
        table(
            [
                ["项目", "内容"],
                ["触发时机", "每次启动，在主界面显示之前"],
                ["版本源", "Gitee penmirror/UPDATE/version"],
                ["下载地址", "UPDATE/BAN/YoudaoPenToolbox_V{版本}.exe（V 大写）"],
                ["下载目录", "%TEMP%\\YoudaoPenToolbox\\update\\"],
                ["安装方式", "apply_update.bat：等待 2s → 覆盖 exe → 启动新版本 → 退出旧进程"],
            ],
            [4 * cm, 12 * cm],
        )
    )
    story.append(Spacer(1, 8))
    story.append(
        warn(
            "检测到新版本时，用户<strong>无法进入旧版主界面</strong>，必须完成更新或退出程序。"
            "更新失败可选择重试或退出。网络检查失败时静默忽略，正常启动。",
            s,
        )
    )
    story.append(PageBreak())

    # Ch14 FAQ
    story.append(p("第十四章  常见问题与故障排除", s["h1"]))
    faq = [
        (
            "Q：刷新设备后列表为空？",
            "A：确认 USB 调试已开启、数据线可传输数据、adb.exe 路径正确。"
            "可在 ADB 终端输入 devices 测试。若设备 offline，重新插拔 USB。",
        ),
        (
            "Q：提示「请先解锁 ADB」？",
            "A：设备 Shell 未开放，需完成第九章 PenNewInject 付费解锁流程。",
        ),
        (
            "Q：应用列表加载失败？",
            "A：通常 packages.json 读取失败。确认 ADB 已解锁且 Shell 可用，查看状态栏错误详情。",
        ),
        (
            "Q：拖入 AMR 无反应？",
            "A：确认扩展名为 .amr（小写），且已选中在线设备、程序非忙碌状态。",
        ),
        (
            "Q：Loli 安装失败「未能识别芯片」？",
            "A：设备平台不在 RK/CVI 自动识别范围，请手动下载对应 AMR 拖入安装。",
        ),
        (
            "Q：ADB 持久化启用后重启仍要授权？",
            "A：先 adb shell auth 成功再启用；启用后点「测试钩子」验证；"
            "确认 /userdisk/skip_re/skip_login.sh 存在且含 YOUDAO_PEN_TOOLBOX_ADB_PERSIST 标记。",
        ),
        (
            "Q：文件预览提示过大？",
            "A：内置查看器限制 2 MB，超大文件请「下载到电脑」后用专业工具打开。",
        ),
        (
            "Q：分区刷写失败？",
            "A：检查镜像大小是否超过分区容量、分区是否应卸载后再刷、USB 连接是否稳定。",
        ),
        (
            "Q：更新下载 404？",
            "A：Gitee 上文件名须为 YoudaoPenToolbox_V1.0.1.exe（V 大写），与 version 文件版本号一致。",
        ),
        (
            "Q：同目录出现 adb.exe 等文件？",
            "A：v1.0.1 首次启动自动释放内置 ADB 组件，正常现象，请勿删除。",
        ),
        (
            "Q：程序崩溃怎么办？",
            "A：查看 exe 同目录 error.log，内含异常堆栈。可至 GitHub Issues 反馈。",
        ),
        (
            "Q：受保护系统应用能否卸载？",
            "A：可以，但会强制自动备份 AMR 并多次确认。卸载可能影响录音、查词、桌面等核心功能，"
            "备份文件可用于尝试拖回安装找回。",
        ),
    ]
    for q, a in faq:
        story.append(p(f"<b>{q}</b>", s["body"]))
        story.append(p(a, s["body"]))
        story.append(Spacer(1, 4))
    story.append(PageBreak())

    # Appendix A
    story.append(p("附录 A  miniapp_cli 命令速查表", s["h1"]))
    cli_rows = [
        ["命令", "分类", "用法", "说明"],
        ["memoryApp", "内存调试", "miniapp_cli memoryApp", "显示进程内存使用"],
        ["trimImageCache", "内存调试", "miniapp_cli trimImageCache", "清理图片内存缓存"],
        ["debugApp", "内存调试", "miniapp_cli debugApp {appid}", "设置调试 App（16 位 AppId）"],
        ["debugService", "内存调试", "miniapp_cli debugService {appid}", "设置调试 Service"],
        ["memoryUsage", "内存调试", "miniapp_cli memoryUsage", "QuickJS 内存（需先 debugApp）"],
        ["memoryUsageGC", "内存调试", "miniapp_cli memoryUsageGC", "GC 后 QuickJS 内存"],
        ["dumpMemory", "内存调试", "miniapp_cli dumpMemory", "导出快照到 /tmp/httpdump.snapshot"],
        ["install", "应用管理", "miniapp_cli install {path}", "安装设备上 AMR 绝对路径"],
        ["uninstall", "应用管理", "miniapp_cli uninstall {appid}", "按 AppId 卸载"],
        ["start", "应用管理", "miniapp_cli start {appId} --{page}", "启动应用页面"],
        ["startService", "应用管理", "miniapp_cli startService {appId} {service}", "启动 Background Service"],
        ["capture", "屏幕工具", "miniapp_cli capture {path}", "截屏到设备 PNG 路径"],
        ["captureFB", "屏幕工具", "miniapp_cli captureFB {path}", "Framebuffer 截屏"],
        ["beginMonkey", "测试", "miniapp_cli beginMonkey", "开始 Monkey 测试（谨慎）"],
        ["stopMonkey", "测试", "miniapp_cli stopMonkey", "停止 Monkey 测试"],
        ["injectKey", "输入模拟", "miniapp_cli injectKey {keyCode}", "注入按键（3=Home 4=Back）"],
        ["setRenderConfig", "系统配置", "miniapp_cli setRenderConfig {key} {value}", "设置渲染配置"],
    ]
    story.append(table(cli_rows, [2.2 * cm, 2 * cm, 5.5 * cm, 6.3 * cm]))
    story.append(PageBreak())

    # Appendix B
    story.append(p("附录 B  受保护系统应用说明", s["h1"]))
    story.append(
        p(
            "以下 37 个 AppId 被标记为「系统·受保护」。界面中类型列显示「系统·受保护」，"
            "卸载列显示「受保护」。卸载时会：",
            s["body"],
        )
    )
    for step in [
        "警告可能影响录音、查词、桌面等核心功能",
        "自动备份 AMR 到 文档\\YoudaoPenToolbox\\SystemAppBackups\\",
        "备份失败时询问是否强制卸载",
        "最终确认后执行卸载，并提示 AMR 应用找回方法",
    ]:
        story.append(bullet(step, s))
    protected_ids = [
        "8001699004677776", "8001712908385557", "8001736586947101", "8001654057944134",
        "8001678699362882", "8001650599023931", "8080252464522508", "8001684209830578",
        "8080212246010681", "8080282263329158", "8080212693101341", "8001659430761211",
        "8001671616562847", "8080292157485624", "8001670668055425", "8001735023580768",
        "8080262605498742", "8080272425914438", "8001661999525016", "8001679380845889",
        "8080232418330628", "8001693795735455", "8080222501178405", "8001718963156066",
        "8001707294117702", "8001657101235091", "8001673244388308", "8080282888534774",
        "8001657592345846", "8080212335092787", "8080212680903142", "8080232030310583",
        "8001666679481944", "8001656491465980", "8001667273038889", "8001660789649766",
        "8001733817189797", "8080222437664451",
    ]
    story.append(Spacer(1, 8))
    story.append(p("受保护 AppId 列表：", s["h3"]))
    # 4 columns
    rows = []
    row = []
    for i, pid in enumerate(protected_ids):
        row.append(pid)
        if len(row) == 3:
            rows.append(row)
            row = []
    if row:
        while len(row) < 3:
            row.append("")
        rows.append(row)
    story.append(table([["AppId", "AppId", "AppId"]] + rows, [5.3 * cm, 5.3 * cm, 5.3 * cm]))
    story.append(PageBreak())

    # Appendix C
    story.append(p("附录 C  重要路径与文件位置", s["h1"]))
    story.append(
        table(
            [
                ["路径", "说明"],
                ["exe 同目录\\adb.exe 等", "首次启动释放的 ADB 组件"],
                ["exe 同目录\\error.log", "程序崩溃日志"],
                ["%AppData%\\YoudaoPenToolbox\\theme.txt", "主题偏好"],
                ["%TEMP%\\YoudaoPenToolbox\\update\\", "更新下载缓存"],
                ["%TEMP%\\YoudaoPenToolbox\\loli\\", "Loli AMR 下载缓存"],
                ["%TEMP%\\YoudaoPenToolbox\\PenNewInject\\", "PenNewInject 下载解压缓存"],
                ["文档\\YoudaoPenToolbox\\PartitionBackups\\", "分区备份默认目录"],
                ["文档\\YoudaoPenToolbox\\SystemAppBackups\\", "受保护系统应用自动备份"],
                ["/userdisk/", "设备用户存储，AMR 安装上传目录"],
                ["/tmp/.adb_auth_verified", "ADB 持久化授权标记"],
                ["/userdisk/skip_re/skip_login.sh", "ADB 持久化开机钩子"],
            ],
            [6 * cm, 10 * cm],
        )
    )
    story.append(PageBreak())

    # Appendix D
    story.append(p("附录 D  免责声明与安全须知", s["h1"]))
    disclaimers = [
        "本工具箱仅供学习与个人设备管理使用。分区刷写、系统应用卸载等操作可能导致设备损坏或数据丢失，"
        "一切后果由用户自行承担。",
        "PenNewInject 为第三方付费解锁工具，与工具箱作者无直接关联；KEY 购买与使用请遵循其服务条款。",
        "从非官方渠道下载的 AMR、分区镜像可能存在安全风险，请仅使用可信来源文件。",
        "ADB 持久化修改设备开机脚本，若与其他工具冲突请谨慎使用。",
        "本软件按 AGPL-3.0 协议开源，不提供任何形式的明示或暗示担保。",
        "使用本软件即表示您已阅读并理解以上条款。",
    ]
    for i, d in enumerate(disclaimers, 1):
        story.append(bullet(f"{i}. {d}", s))
    story.append(Spacer(1, 1 * cm))
    story.append(
        p(
            "— 文档结束 —<br/><br/>"
            f"有道词典笔工具箱 v{VERSION} 用户手册<br/>"
            "https://github.com/mirror2008/YoudaoPenToolbox<br/>"
            "制作不易，且行且珍惜。",
            s["subtitle"],
        )
    )

    return story


def main():
    register_fonts()
    styles = build_styles()
    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)

    doc = BaseDocTemplate(
        OUTPUT,
        pagesize=A4,
        leftMargin=2 * cm,
        rightMargin=2 * cm,
        topMargin=2 * cm,
        bottomMargin=2 * cm,
        title=f"有道词典笔工具箱 v{VERSION} 用户手册",
        author="MIRROR",
    )
    frame = Frame(doc.leftMargin, doc.bottomMargin, doc.width, doc.height - 0.8 * cm, id="normal")
    doc.addPageTemplates([PageTemplate(id="main", frames=[frame], onPage=header_footer)])
    doc.build(build_story(styles))
    print(f"Generated: {OUTPUT}")
    print(f"Size: {os.path.getsize(OUTPUT):,} bytes")


if __name__ == "__main__":
    main()
