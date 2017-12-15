#pragma once
#define IMAGEAPI __stdcall
#define SYMOPT_CASE_INSENSITIVE          0x00000001
#define SYMOPT_UNDNAME                   0x00000002
#define SYMOPT_DEFERRED_LOADS            0x00000004
#define SYMOPT_NO_CPP                    0x00000008
#define SYMOPT_LOAD_LINES                0x00000010
#define SYMOPT_OMAP_FIND_NEAREST         0x00000020
#define SYMOPT_LOAD_ANYTHING             0x00000040
#define SYMOPT_IGNORE_CVREC              0x00000080
#define SYMOPT_NO_UNQUALIFIED_LOADS      0x00000100
#define SYMOPT_FAIL_CRITICAL_ERRORS      0x00000200
#define SYMOPT_EXACT_SYMBOLS             0x00000400
#define SYMOPT_ALLOW_ABSOLUTE_SYMBOLS    0x00000800
#define SYMOPT_IGNORE_NT_SYMPATH         0x00001000
#define SYMOPT_INCLUDE_32BIT_MODULES     0x00002000
#define SYMOPT_PUBLICS_ONLY              0x00004000
#define SYMOPT_NO_PUBLICS                0x00008000
#define SYMOPT_AUTO_PUBLICS              0x00010000
#define SYMOPT_NO_IMAGE_SEARCH           0x00020000
#define SYMOPT_SECURE                    0x00040000
#define SYMOPT_NO_PROMPTS                0x00080000
#define SYMOPT_OVERWRITE                 0x00100000
#define SYMOPT_IGNORE_IMAGEDIR           0x00200000
#define SYMOPT_FLAT_DIRECTORY            0x00400000
#define SYMOPT_FAVOR_COMPRESSED          0x00800000
#define SYMOPT_ALLOW_ZERO_ADDRESS        0x01000000
#define SYMOPT_DISABLE_SYMSRV_AUTODETECT 0x02000000
#define SYMOPT_READONLY_CACHE            0x04000000
#define SYMOPT_SYMPATH_LAST              0x08000000
#define SYMOPT_DISABLE_FAST_SYMBOLS      0x10000000
#define SYMOPT_DISABLE_SYMSRV_TIMEOUT    0x20000000
#define SYMOPT_DISABLE_SRVSTAR_ON_STARTUP 0x40000000
#define SYMOPT_DEBUG                     0x80000000

typedef enum {
	AddrMode1616,
	AddrMode1632,
	AddrModeReal,
	AddrModeFlat
} ADDRESS_MODE;

typedef struct _tagADDRESS64 {
	DWORD64       Offset;
	WORD          Segment;
	ADDRESS_MODE  Mode;
} ADDRESS64, *LPADDRESS64;

typedef struct _KDHELP64 {
	DWORD64   Thread;
	DWORD   ThCallbackStack;
	DWORD   ThCallbackBStore;
	DWORD   NextCallback;
	DWORD   FramePointer;
	DWORD64   KiCallUserMode;
	DWORD64   KeUserCallbackDispatcher;
	DWORD64   SystemRangeStart;
	DWORD64   KiUserExceptionDispatcher;
	DWORD64   StackBase;
	DWORD64   StackLimit;
	DWORD     BuildVersion;
	DWORD     Reserved0;
	DWORD64   Reserved1[4];

} KDHELP64, *PKDHELP64;

typedef struct _tagSTACKFRAME64 {
	ADDRESS64   AddrPC;               // program counter
	ADDRESS64   AddrReturn;           // return address
	ADDRESS64   AddrFrame;            // frame pointer
	ADDRESS64   AddrStack;            // stack pointer
	ADDRESS64   AddrBStore;           // backing store pointer
	PVOID       FuncTableEntry;       // pointer to pdata/fpo or NULL
	DWORD64     Params[4];            // possible arguments to the function
	BOOL        Far;                  // WOW far call
	BOOL        Virtual;              // is this a virtual frame?
	DWORD64     Reserved[3];
	KDHELP64    KdHelp;
} STACKFRAME64, *LPSTACKFRAME64;

typedef struct _SYMBOL_INFO {
	ULONG   SizeOfStruct;
	ULONG   TypeIndex;
	ULONG64 Reserved[2];
	ULONG   Index;
	ULONG   Size;
	ULONG64 ModBase;
	ULONG   Flags;
	ULONG64 Value;
	ULONG64 Address;
	ULONG   Register;
	ULONG   Scope;
	ULONG   Tag;
	ULONG   NameLen;
	ULONG   MaxNameLen;
	CHAR   Name[1];
} SYMBOL_INFO, *PSYMBOL_INFO;

typedef struct _IMAGEHLP_LINE64 {
	DWORD    SizeOfStruct;           // set to sizeof(IMAGEHLP_LINE64)
	PVOID    Key;                    // internal
	DWORD    LineNumber;             // line number in file
	PCHAR    FileName;               // full filename
	DWORD64  Address;                // first instruction of line
} IMAGEHLP_LINE64, *PIMAGEHLP_LINE64;

typedef struct _IMAGEHLP_SYMBOL64 {
	DWORD   SizeOfStruct;           // set to sizeof(IMAGEHLP_SYMBOL64)
	DWORD64 Address;                // virtual address including dll base address
	DWORD   Size;                   // estimated size of symbol, can be zero
	DWORD   Flags;                  // info about the symbols, see the SYMF defines
	DWORD   MaxNameLength;          // maximum size of symbol name in 'Name'
	CHAR    Name[1];                // symbol name (null terminated string)
} IMAGEHLP_SYMBOL64, *PIMAGEHLP_SYMBOL64;

typedef BOOL(IMAGEAPI *PREAD_PROCESS_MEMORY_ROUTINE64)(_In_ HANDLE hProcess, _In_ DWORD64 qwBaseAddress, _Out_writes_bytes_(nSize) PVOID lpBuffer, _In_ DWORD nSize, _Out_ LPDWORD lpNumberOfBytesRead);
typedef PVOID(IMAGEAPI *PFUNCTION_TABLE_ACCESS_ROUTINE64)(_In_ HANDLE ahProcess, _In_ DWORD64 AddrBase);
typedef DWORD64(IMAGEAPI *PGET_MODULE_BASE_ROUTINE64)(_In_ HANDLE hProcess, _In_ DWORD64 Address);
typedef DWORD64(IMAGEAPI *PTRANSLATE_ADDRESS_ROUTINE64)(_In_ HANDLE hProcess, _In_ HANDLE hThread, _In_ LPADDRESS64 lpaddr);

BOOL IMAGEAPI StackWalk64(_In_ DWORD MachineType, _In_ HANDLE hProcess, _In_ HANDLE hThread, _Inout_  LPSTACKFRAME64 StackFrame,
	_Inout_  PVOID ContextRecord, _In_opt_ PREAD_PROCESS_MEMORY_ROUTINE64 ReadMemoryRoutine, _In_opt_ PFUNCTION_TABLE_ACCESS_ROUTINE64 FunctionTableAccessRoutine,
	_In_opt_ PGET_MODULE_BASE_ROUTINE64 GetModuleBaseRoutine, _In_opt_ PTRANSLATE_ADDRESS_ROUTINE64 TranslateAddress);
BOOL IMAGEAPI SymInitialize( _In_ HANDLE hProcess, _In_opt_ LPCSTR UserSearchPath, _In_ BOOL fInvadeProcess);
BOOL IMAGEAPI SymCleanup(_In_ HANDLE hProcess);
DWORD IMAGEAPI SymSetOptions(_In_ DWORD SymOptions);
DWORD IMAGEAPI SymGetOptions(VOID);
BOOL IMAGEAPI SymFromAddr(_In_ HANDLE hProcess, _In_ DWORD64 Address, _Out_opt_ PDWORD64 Displacement, _Inout_ PSYMBOL_INFO Symbol);
BOOL WINAPI SymGetSymFromAddr64(_In_ HANDLE hProcess, _In_ DWORD64 Address, _Out_opt_ PDWORD64 Displacement, _Inout_ PIMAGEHLP_SYMBOL64 Symbol);
BOOL IMAGEAPI SymGetLineFromAddr64(_In_ HANDLE hProcess, _In_ DWORD64 dwAddr, _Out_ PDWORD pdwDisplacement, _Out_ PIMAGEHLP_LINE64 Line);

VOID WINAPI RtlCaptureContext(_Out_ PCONTEXT ContextRecord);
BOOL WINAPI SetThreadContext( _In_ HANDLE hThread, _In_ const CONTEXT* lpContext);