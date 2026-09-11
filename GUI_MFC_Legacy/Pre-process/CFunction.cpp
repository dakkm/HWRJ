#include"pch.h"
#include"CFunction.h"
void CStringToUtf8CharArr(const CString& strSrc, char* szdst, int nBufLen)
{
	memset(szdst, 0, nBufLen);
	int nutf8len = WideCharToMultiByte(CP_UTF8, 0, strSrc, -1, NULL, 0, NULL, NULL);
	if (nutf8len <= 0 || nutf8len >= nBufLen)
	{
		return;
	}
	WideCharToMultiByte(CP_UTF8, 0, strSrc, -1, szdst, nBufLen - 1, NULL, NULL);
}
CString Utf8CharArrToCString(const char* szutf8)
{
	CString strRet;
	if (!szutf8 || *szutf8 == 0)
	{
		return strRet;
	}
	int nwlen = MultiByteToWideChar(CP_UTF8, 0, szutf8, -1, NULL, 0);
	if (nwlen <= 0)
	{
		return strRet;
	}
	WCHAR* pwbuf = new WCHAR[nwlen];
	MultiByteToWideChar(CP_UTF8, 0, szutf8, -1, pwbuf, nwlen);

	CStringW strW(pwbuf);

	strRet = strW;
	delete[] pwbuf;
	return strRet;
}
BOOL RunPythonScript(const CString& pyExePath, const CString& pyScriptPath)
{
	STARTUPINFO si = { 0 };
	PROCESS_INFORMATION pi = { 0 };
	si.cb = sizeof(si);
	CString cmdline;
	cmdline.Format(_T("\"%s\" \"%s\""), pyExePath, pyScriptPath);
	DWORD dwCreateFlag = CREATE_NO_WINDOW;
	CString strPyWorkDir = pyScriptPath;
	int nlastSlash = strPyWorkDir.ReverseFind(_T('/'));
	if (strPyWorkDir != -1)
	{
		strPyWorkDir = strPyWorkDir.Left(nlastSlash);
	}
	BOOL bOk = CreateProcess(NULL, cmdline.GetBuffer(), NULL, NULL, FALSE, dwCreateFlag, NULL, strPyWorkDir.GetBuffer(), &si, &pi);
	if (!bOk)
	{
		DWORD err = GetLastError();
		AfxMessageBox(_T("CreateProcessÊ§°Ü£¬´íÎóÂë£º") + CString(std::to_string(err).c_str()));
		return FALSE;
	}
	WaitForSingleObject(pi.hProcess, INFINITE);
	DWORD dwExitCode;
	GetExitCodeProcess(pi.hProcess, &dwExitCode);
	CloseHandle(pi.hThread);
	CloseHandle(pi.hProcess);
	return TRUE;
}
BOOL RunPythonScript_total (CString cmdline,CString strPyWorkDir)
{
	STARTUPINFO si = { 0 };
	PROCESS_INFORMATION pi = { 0 };
	si.cb = sizeof(si);
	//DWORD dwCreateFlag = CREATE_NO_WINDOW;
	DWORD dwCreateFlag = 0;
	//BOOL bOk = CreateProcess(NULL, cmdline.GetBuffer(), NULL, NULL, FALSE, dwCreateFlag, NULL, strPyWorkDir.GetBuffer(), &si, &pi);
	BOOL bOk = CreateProcess(NULL, cmdline.GetBuffer(), NULL, NULL, FALSE, dwCreateFlag, NULL, strPyWorkDir.GetBuffer(), &si, &pi);
	if (!bOk)
	{
		DWORD err = GetLastError();
		AfxMessageBox(_T("CreateProcessÊ§°Ü£¬´íÎóÂë£º") + CString(std::to_string(err).c_str()));
		return FALSE;
	}
	WaitForSingleObject(pi.hProcess, INFINITE);
	DWORD dwExitCode;
	GetExitCodeProcess(pi.hProcess, &dwExitCode);
	CloseHandle(pi.hThread);
	CloseHandle(pi.hProcess);
	return TRUE;
}
std::vector<std::string>splitbycomma(const std::string& line)
{
	std::vector<std::string> fields;
	std::string  field;
	for (char ch : line)
	{
		if (ch == ',')
		{
			fields.push_back(field);
			field.clear();
		}
		else
		{
			field += ch;
		}
	}
	fields.push_back(field);
	return fields;
}