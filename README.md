# ValleyVision（星露视界）

ValleyVision 是独立的 Stardew Valley SMAPI 模组。在原版电视频道菜单中选择“播放在线视频”，输入公开的 B 站或 YouTube 普通视频链接，即可在当前电视上播放画面和声音。原版频道仍可使用。

支持 B 站 BV 视频页、`b23.tv` 短链及有效的 `?p=` 分 P；支持 HTTPS `youtube.com/watch?v=...`（包括 `www`、`m`）和 `youtu.be/...` 单视频链接。YouTube 链接即使带有播放列表参数，也只播放指定视频。暂不支持 Shorts、直播、仅列表页、登录后可见、年龄限制、会员或付费内容。

## 安装与使用

1. 安装 Stardew Valley 和 SMAPI 4.5.2 或更新版本。
2. 将构建得到的 `ValleyVision.dll` 与 `manifest.json` 放入游戏 `Mods/ValleyVision` 目录。
3. 自行安装 Windows 版 `yt-dlp.exe`、`ffmpeg.exe` 和 `ffplay.exe`，并加入 `PATH`，或在模组目录的 `config.json` 中填写其绝对路径。模组包不包含这些工具，也不需要 Python。
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

前三个路径留空或设为 `null` 时从 `PATH` 查找。YouTube 可选用 `deno` 或 `node` 作为 JavaScript 运行环境；留空时由 yt-dlp 默认探测。指定运行环境路径时也必须填写类型。请参阅 [yt-dlp 官方 EJS 指南](https://github.com/yt-dlp/yt-dlp/wiki/EJS) 安装匹配的运行环境。模组不会读取浏览器 Cookie、自动下载工具或持久保存视频链接。声音由外部 `ffplay` 播放，暂不与游戏音量滑块联动。

视频结束、停止、离开场景或返回标题时会清理播放会话；工具缺失、解析失败或播放中断时，游戏会显示错误提示。

## 从源码构建

项目目标框架为 .NET 6，使用 `Pathoschild.Stardew.ModBuildConfig` 4.4.0。复制 `ValleyVision.Local.props.example` 为 `ValleyVision.Local.props`，将 `GamePath` 改为本机 Stardew Valley 安装目录，再运行：

```powershell
dotnet restore ValleyVision.sln
dotnet build ValleyVision.sln --configuration Release
```

普通构建默认不向游戏目录部署。`ValleyVision.Local.props`、构建产物及本机配置均已被 Git 忽略。
