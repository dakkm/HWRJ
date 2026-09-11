#include<afx.h>
#include<cstring>
#include<string>
#include<Windows.h>
#include<vector>

void CStringToUtf8CharArr(const CString& strSrc, char* szdst, int nBufLen);
CString Utf8CharArrToCString(const char* szutf8);
BOOL RunPythonScript(const CString& pyExePath, const CString& pyScriptPath);
BOOL RunPythonScript_total(const CString cmdline, CString strPyWorkDir);
std::vector<std::string>splitbycomma(const std::string& line);