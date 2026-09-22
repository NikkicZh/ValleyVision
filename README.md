# ValleyVision（星露视界）

简体中文 | [English](README.en.md)

ValleyVision 是独立的 Stardew Valley SMAPI 模组。在原版电视频道菜单中选择“播放在线视频”，输入公开的 B 站或 YouTube 普通视频链接，即可在当前电视上播放画面和声音。模组新增界面文字目前只有中文；原版频道仍可使用。源码采用 [MIT 许可证](LICENSE)。

支持 B 站 BV 视频页、有效的 `b23.tv` 短链及有效的 `?p=` 分 P；支持 HTTPS `youtube.com/watch?v=...`（包括 `www`、`m`）和 `youtu.be/...` 单视频链接。YouTube 链接即使带有播放列表参数，也只从头播放指定视频，不连续播放列表。暂不支持 Shorts、直播、仅列表页、登录后可见、年龄限制、会员或付费内容，也不处理 PO Token。

目前只在 Windows Steam 游戏 1.6.15、SMAPI 4.5.2 的本机环境测试过开发构建；其他平台和版本尚未验证。最终发布包仍需单独做游戏内验收。

## 游戏截图

以下截图来自 Windows 版游戏的 ValleyVision 开发构建，展示链接输入、YouTube 播放和电视菜单中的停止选项。截图不代表最终发布包已完成验收。

![电视上的在线视频链接输入框](images/link-input.png)

![YouTube 视频在游戏电视上播放的画面与提示](images/youtube-playback.png)

![电视原版频道菜单中的“停止在线视频”选项](images/tv-channel-menu.png)

## 安装与使用

1. 安装 Stardew Valley 和 SMAPI 4.5.2 或更新版本。
2. 将安装包中的 `ValleyVision` 文件夹解压到游戏 `Mods` 目录，使 `Mods/ValleyVision` 直接包含 `ValleyVision.dll` 与 `manifest.json`。
3. 自行安装 Windows 版 [yt-dlp](https://github.com/yt-dlp/yt-dlp/wiki/Installation) 的 `yt-dlp.exe`，以及可提供 `ffmpeg.exe` 和 `ffplay.exe` 的 [FFmpeg Windows 构建](https://ffmpeg.org/download.html)；将三个程序加入 `PATH`，或在模组目录的 `config.json` 中填写其绝对路径。模组包不包含这些工具，使用官方 `yt-dlp.exe` 不需要 Python。
4. 进入游戏，点击电视并选择“播放在线视频”。播放中再次点击电视可以停止；选择原版频道也会停止当前视频。

配置示例：

```json
{
  "YtDlpPath": "C:\\Tools\\yt-dlp.exe",
  "FfmpegPath": "C:\\Tools\\ffmpeg.exe",
  "FfplayPath": "C:\\Tools\\ffplay.exe",
  "YouTubeJsRuntime": "deno",
  "YouTubeJsRuntimePath": "C:\\Tools\\deno.exe"
}
```

前三个路径留空或设为 `null` 时从 `PATH` 查找。YouTube 提取可能需要 JavaScript 运行环境；可选 `deno` 或 `node`，留空时由 yt-dlp 默认探测。使用 Node 时需 22 或更新版本，并在上述配置中明确启用。指定运行环境路径时也必须填写类型。请参阅 [yt-dlp 官方 EJS 指南](https://github.com/yt-dlp/yt-dlp/wiki/EJS) 安装匹配的运行环境；官方 `yt-dlp.exe` 已包含 EJS 组件。模组使用 `--ignore-config`，不会读取用户 yt-dlp 配置文件或浏览器 Cookie，不自动下载工具或 EJS，不持久保存视频链接，也不记录完整媒体地址。声音由外部 `ffplay` 播放，暂不与游戏音量滑块联动。

视频结束、停止、离开场景或返回标题时会清理播放会话；工具缺失、解析失败或播放中断时，游戏会显示错误提示。

## 卸载与排查

关闭游戏后删除 `Mods/ValleyVision` 文件夹即可卸载。播放失败时，先运行 `yt-dlp --version`、`ffmpeg -version`、`ffplay -version` 检查工具，再核对配置路径、视频访问权限及 YouTube EJS 环境。反馈问题时请提供脱敏的 SMAPI 日志与复现步骤，避免公开私人链接或媒体地址。

## 从源码构建

项目目标框架为 .NET 6，使用 `Pathoschild.Stardew.ModBuildConfig` 4.4.0。复制 `ValleyVision.Local.props.example` 为 `ValleyVision.Local.props`，将 `GamePath` 改为本机 Stardew Valley 安装目录，再运行：

```powershell
dotnet restore ValleyVision.sln
dotnet build ValleyVision.sln --configuration Release
```

普通构建默认不向游戏目录部署。`ValleyVision.Local.props`、构建产物及本机配置均已被 Git 忽略。
