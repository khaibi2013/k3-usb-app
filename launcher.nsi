Name "IronVault"
OutFile "release/IronVault_Start.exe"
SilentInstall silent
RequestExecutionLevel user

Section "MainSection" SEC01
  Exec '"$EXEDIR\K3_System\IronVault.exe"'
SectionEnd
