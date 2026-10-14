# Chrome Remote Desktop 浮窗控制

這是一個 Windows 系統匣工具，用來隱藏或重新定位 Chrome Remote Desktop「你目前正與 ... 共用電腦」提示。

它使用 `EnumWindows`、`ShowWindow` 和 `SetWindowPos`，優先辨識 Chrome Remote Desktop 原生提示的控制項，再以分享狀態文字作後備判斷。它不注入 `chrome.exe`、不修改 Chrome 檔案，也不會停止 Chrome Remote Desktop 服務。

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

## 原生診斷測試

```powershell
dotnet run --project tests\ProcessDiagnosticsSmoke\ProcessDiagnosticsSmoke.csproj -c Release
```

測試完整性層級分類、自身 token / UIAccess 查詢、無效 PID 和重複查詢的 handle 釋放；不代表已驗證其他電腦上的 CRD 浮窗操作。

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
執行期間的診斷紀錄位於 `%LOCALAPPDATA%\ChromeRemoteBarHide\debug.log`，記錄熱鍵、視窗操作結果、程序名稱、工作階段、完整性層級及 UIAccess，不會記錄帳號或畫面內容。
`Matched sharing bar but could not hide it` 只代表操作後視窗仍可見，不能單憑這行認定 Windows 拒絕操作。請提供 `Controller security`、`Observed sharing bar` 與失敗行；`SetWindowPos success=False; Win32 error=5` 才是該次操作遭拒的證據。若回傳成功但仍可見，也可能是目標程序重新顯示視窗。
程式只處理分享狀態列，不隱藏連線授權或「是否繼續共用」確認視窗。

## 安全邊界

程式以目前使用者權限執行（`asInvoker`），不自動要求管理員權限。若要比較權限影響，可在被連入的電腦先結束普通版，再由電腦使用者右鍵「以系統管理員身分執行」，由本人確認 UAC；這是診斷步驟，不保證解決問題。請將兩次執行的 log 一起比較。
管理員程序通常是 `High`，並不等於 `System`；若目標層級更高，不應假定管理員模式就能操作它。本工具不取得 SYSTEM 權限、不注入 CRD 程序、不關閉 UAC，也不修改 CRD 服務。
