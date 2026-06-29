# EditorSimple

Editor teks **ringan & cepat** untuk Windows, bergaya **VSCode** (dark, berwarna).
Dibangun dengan **C# WPF + [AvalonEdit](https://github.com/icsharpcode/AvalonEdit)**.
Cocok untuk membuka & mengedit file config (JSON / YAML / TOML) dan Markdown.

Tujuan: sekadar buka → edit → simpan, dengan sidebar file tree untuk pindah cepat antar folder/file dan tab multi-file.

---

## Build & Run

```powershell
cd "D:\Dari Desktop\Droid\editorsimple"
dotnet run -c Debug
```

Diperlukan **.NET 6 SDK** (sudah terpasang di sistem ini). Tidak perlu langkah lain.

## Shortcut

| Aksi | Pintasan |
|------|----------|
| New file | `Ctrl+N` |
| Open File | `Ctrl+O` |
| Open Folder | `Ctrl+Shift+O` |
| Save | `Ctrl+S` |
| Save As | `Ctrl+Shift+S` |
| Close Tab | `Ctrl+W` |
| Pindah tab | `Ctrl+Tab` / `Ctrl+Shift+Tab` |
| Toggle word wrap | menu *View → Toggle Word Wrap* |

Tab yang belum disimpan ditandai `●`; saat menutup / keluar akan diminta konfirmasi.

## Layout

```
Menu | sidebar EXPLORER (file tree) | editor (tab, line number, berwarna) | status bar (Ln/Col, format, ●)
```

## Syntax highlighting

Format berwarna (tema gelap VSCode): **JSON, YAML, TOML, Markdown**.
Definisi ada di `Syntax/*.xshd` (di-embed ke exe). File lain (`.txt`, `.ini`, `.env`, …) tampil plain di latar gelap.

## Publikasi 1 file EXE portabel

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Hasil: `bin\Release\net6.0-windows\win-x64\publish\EditorSimple.exe` (butuh .NET runtime di sistem).

Untuk exe mandiri tanpa runtime sistem, tambahkan `--self-contained true`.

## Coba cepat

Buka folder `samples/` di dalam aplikasi (menu *File → Open Folder* atau `Ctrl+Shift+O`) lalu klik file-nya.

## Opsional: naik ke .NET 8 (LTS)

```powershell
winget install Microsoft.DotNet.SDK.8
```
Lalu ubah `<TargetFramework>` menjadi `net8.0-windows` di `EditorSimple.csproj` (tidak mengubah kode lain).
