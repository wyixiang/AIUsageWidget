Option Explicit
Dim sh, fso, baseDir
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
baseDir = fso.GetParentFolderName(WScript.ScriptFullName)
sh.CurrentDirectory = baseDir
sh.Run Chr(34) & fso.BuildPath(baseDir, "AIUsageWidget.exe") & Chr(34), 1, False
