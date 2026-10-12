# Chrome Remote Desktop 浮窗控制

這是一個 Windows 系統匣工具，用來隱藏或重新定位 Chrome Remote Desktop「你目前正與 ... 共用電腦」提示。

它使用 `EnumWindows`、`ShowWindow` 和 `SetWindowPos`，只處理同時包含分享狀態文字及 `Stop Sharing`/`停止共用` 按鈕文字的小型提示視窗。它不注入 `chrome.exe`、不修改 Chrome 檔案，也不會停止 Chrome Remote Desktop 服務。

## 編譯

需要 .NET 8 SDK 和 Windows：

```powershell
dotnet build -c Release -p:TargetPlatformDisplayName=Windows
```

執行檔位於 `bin\Release\net8.0-windows\ChromeRemoteBarHide.exe`。

## 傳給其他電腦

一般 Intel/AMD Windows 11 請使用 `dist\ChromeRemoteBarHide-win-x64.zip`。Windows 11 沒有 32 位元版。
ZIP 必須完整解壓縮，保留 EXE 旁邊的 DLL、deps.json 和 runtimeconfig.json，然後在被連入的主機執行 EXE。
這是依賴執行階段的版本，需安裝 .NET 8 Desktop Runtime（x64 版本配 x64 Runtime；x86 版本配 x86 Runtime）。
官方下載：https://dotnet.microsoft.com/en-us/download/dotnet/8.0

已完成編譯與執行檔架構檢查，尚未驗證實際 Chrome Remote Desktop 連線中的浮窗；新版 UI 可能需要調整偵測方式。

## 操作

- 啟動後預設自動隱藏提示。
- 右鍵系統匣圖示可選擇自動隱藏、上/下/左/右定位，或停用並顯示。
- `Ctrl+Alt+H`：強制啟用自動隱藏。
- `Ctrl+Alt+R`：停用並顯示提示。若 `Ctrl+Alt+R` 已被其他程式占用，啟動時會自動改用 `Ctrl+Alt+Shift+R`，托盤選單會顯示實際快捷鍵。
- `Ctrl+Alt+T/B/L/K`：移到上/下/左/右側。
- 雙擊系統匣圖示也會切換隱藏。

Chrome Remote Desktop 若重新建立提示視窗，工具會在下一次掃描（約 0.5 秒）重新套用模式。

## 故障診斷

右鍵系統匣圖示時，選單會顯示熱鍵註冊數量及替代快捷鍵。若不是 `6/6`，代表某些熱鍵已被其他程式占用。
執行期間的診斷紀錄位於 `%LOCALAPPDATA%\ChromeRemoteBarHide\debug.log`，內容只包含熱鍵註冊、例外與符合視窗數量，不會記錄帳號或畫面內容。

## 安全邊界

程式以目前使用者權限執行（`asInvoker`），沒有管理員權限要求。若 Chrome Remote Desktop 以較高權限執行，Windows 可能拒絕跨完整性層級的視窗操作；這時應讓兩者以相同使用者權限執行，而不是把工具改成管理員模式。
