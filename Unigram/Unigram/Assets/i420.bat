set INCLUDEPATH="%WindowsSdkDir%\Include\%WindowsSDKVersion%\um"

fxc i420.hlsl /nologo /T lib_4_0_level_9_3_ps_only /D D2D_FUNCTION /D D2D_ENTRY=main /Fl i420.fxlib /I %INCLUDEPATH%
fxc i420.hlsl /nologo /T ps_4_0_level_9_3 /D D2D_FULL_SHADER /D D2D_ENTRY=main /E main /setprivate i420.fxlib /Fo:i420.bin /I %INCLUDEPATH%