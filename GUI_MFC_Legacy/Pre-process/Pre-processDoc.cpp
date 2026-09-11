
// Pre-processDoc.cpp: CPreprocessDoc 类的实现
//
#include "pch.h"
#include "framework.h"
// SHARED_HANDLERS 可以在实现预览、缩略图和搜索筛选器句柄的
// ATL 项目中进行定义，并允许与该项目共享文档代码。
#ifndef SHARED_HANDLERS
#include "Pre-process.h"
#endif
#include "Pre-processDoc.h"
#include "Pre-processView.h"
#include "CDlg_Simulating_Parameter.h"
#include"CDlg_similary_predict.h"
#include"CFunction.h"
#include<fstream>
#include <propkey.h>
#ifdef _DEBUG
#define new DEBUG_NEW
#endif

// CPreprocessDoc

IMPLEMENT_DYNCREATE(CPreprocessDoc, CDocument)

BEGIN_MESSAGE_MAP(CPreprocessDoc, CDocument)
	//ON_COMMAND(ID_start_forward_simulation, &CPreprocessDoc::Onstartforwardsimulation)
	///ON_COMMAND(ID_start_surrogate_prediction, &CPreprocessDoc::Onstartsurrogateprediction)
	//ON_COMMAND(ID_start_similarity_predict, &CPreprocessDoc::Onstartsimilaritypredict)
	//ON_COMMAND(ID_start_infrared_situation_constructing, &CPreprocessDoc::Onstartinfraredsituationconstructing)
END_MESSAGE_MAP()
// CPreprocessDoc 构造/析构
CPreprocessDoc::CPreprocessDoc()noexcept:m_target_cscv(""),m_candidate_cscv(""),m_target_id(""),m_candidate_id("") 
{
	m_q_int=300;
	m_emissity_ir=0.95;
	m_absorptivity_solar=0.95;
	m_simulation_mode = 0;
	m_predict_mode=0;	
	m_hPythonProcess1 = NULL;
	m_bIsIsPyRunning1 = FALSE;
	m_hPythonProcess2 = NULL;
	m_bIsIsPyRunning2 = FALSE;
	m_hPythonProcess3 = NULL;
	m_bIsIsPyRunning3 = FALSE;
	m_hPythonProcess4 = NULL;
	m_bIsIsPyRunning4 = FALSE;
}
CPreprocessDoc::~CPreprocessDoc()
{

}
BOOL CPreprocessDoc::OnNewDocument()
{
	if (!CDocument::OnNewDocument())
		return FALSE;
 m_task=BusinessTask();
 UpdateAllViews(nullptr);
	return TRUE;
}
void CPreprocessDoc::Serialize(CArchive& ar)
{
 if(ar.IsStoring()) m_task.Serialize(ar);
 else { BusinessTask loaded; loaded.Serialize(ar); m_task=loaded; }
}
BOOL CPreprocessDoc::OnSaveDocument(LPCTSTR path)
{
 POSITION pos=GetFirstViewPosition();
 while(pos) { auto view=DYNAMIC_DOWNCAST(CPreprocessView,GetNextView(pos)); if(view && !view->m_taskEditor.Commit()) return FALSE; }
 m_task.targetMotion.resize(m_task.task.targetCount);
 return CDocument::OnSaveDocument(path);
}

#ifdef SHARED_HANDLERS
// 缩略图的支持
void CPreprocessDoc::OnDrawThumbnail(CDC& dc, LPRECT lprcBounds)
{
	// 修改此代码以绘制文档数据
	dc.FillSolidRect(lprcBounds, RGB(255, 255, 255));
	CString strText = _T("TODO: implement thumbnail drawing here");
	LOGFONT lf;
	CFont* pDefaultGUIFont = CFont::FromHandle((HFONT) GetStockObject(DEFAULT_GUI_FONT));
	pDefaultGUIFont->GetLogFont(&lf);
	lf.lfHeight = 36;
	CFont fontDraw;
	fontDraw.CreateFontIndirect(&lf);
	CFont* pOldFont = dc.SelectObject(&fontDraw);
	dc.DrawText(strText, lprcBounds, DT_CENTER | DT_WORDBREAK);
	dc.SelectObject(pOldFont);
}
// 搜索处理程序的支持
void CPreprocessDoc::InitializeSearchContent()
{
	CString strSearchContent;
	// 从文档数据设置搜索内容。
	// 内容部分应由“;”分隔

	// 例如:     strSearchContent = _T("point;rectangle;circle;ole object;")；
	SetSearchContent(strSearchContent);
}
void CPreprocessDoc::SetSearchContent(const CString& value)
{
	if (value.IsEmpty())
	{
		RemoveChunk(PKEY_Search_Contents.fmtid, PKEY_Search_Contents.pid);
	}
	else
	{
		CMFCFilterChunkValueImpl *pChunk = nullptr;
		ATLTRY(pChunk = new CMFCFilterChunkValueImpl);
		if (pChunk != nullptr)
		{
			pChunk->SetTextValue(PKEY_Search_Contents, value, CHUNK_TEXT);
			SetChunkValue(pChunk);
		}
	}
}
#endif // SHARED_HANDLERS
#ifdef _DEBUG
void CPreprocessDoc::AssertValid() const
{
	CDocument::AssertValid();
}
void CPreprocessDoc::Dump(CDumpContext& dc) const
{
	CDocument::Dump(dc);
}
#endif //_DEBUG
void CPreprocessDoc::OnSimulatingParameter()
{
	CDlg_Simulating_Parameter dlg;
	dlg.m_q_int = m_q_int;
	dlg.m_emissivity_ir = m_emissity_ir;
	dlg.m_absorptivity_solar = m_absorptivity_solar;
	dlg.m_mode = m_simulation_mode;
	dlg.m_simulation_type = m_simulation_type;
	if (dlg.DoModal() == IDOK)
	{
		m_q_int =dlg. m_q_int;
		m_emissity_ir = dlg.m_emissivity_ir;
		m_absorptivity_solar = dlg.m_absorptivity_solar;
		m_simulation_mode=dlg.m_mode;
	}
}
void CPreprocessDoc::Onstartforwardsimulation()
{
	m_simulation_type = 0;
	OnSimulatingParameter();
	//std::ofstream f("request.json");
	//f << "{" << std::endl;
	//f << "\"q_int\": " << m_q_int << std::endl;
	//f << "\"emissivity_ir\": " << m_emissity_ir << std::endl;
	//f << "\"absorptivity_solar\": " << m_absorptivity_solar << std::endl;
	//f << "}" << std::endl;
	//f.close();
}
void CPreprocessDoc::Onstartsurrogateprediction()
{
	m_simulation_type = 1;
	OnSimulatingParameter();	
	std::ofstream f("coreprogram/02-智能预测/01-输入文件/request.json");
	f << "{" << std::endl;
	f << "\"q_int\": " << m_q_int << "," << std::endl;
	f << "\"emissivity_ir\": " << m_emissity_ir << "," << std::endl;
	f << "\"absorptivity_solar\": " << m_absorptivity_solar << std::endl;
	f << "}" << std::endl;
	f.close();
}
bool CPreprocessDoc::Onstartsimilaritypredict()
{
	CDlg_similary_predict dlg;
	//dlg.m_String_file1 =Utf8CharArrToCString(m_target_cscv);
	//dlg.m_string_file2=Utf8CharArrToCString(m_candidate_cscv);
	dlg.m_mode= m_predict_mode;
	if (dlg.DoModal() == IDOK)
	{
		CStringToUtf8CharArr(dlg.m_String_file1, m_target_cscv, 128);
		CStringToUtf8CharArr(dlg.m_string_file2, m_candidate_cscv, 128);
		CStringToUtf8CharArr(dlg.m_String_file1, m_target_cscv, 128);
		CStringToUtf8CharArr(dlg.m_string_file2, m_candidate_cscv, 128);
		snprintf(m_target_id,128, dlg.m_targetIDs[dlg.m_select_target].c_str());
		snprintf(m_candidate_id, 128, dlg.m_candidateIDs[dlg.m_select_candidate].c_str());
		return true;
	}
	return false;
}
void CPreprocessDoc::Onstartinfraredsituationconstructing()
{
	CString exepath_full = _T("coreprogram/04-红外场景构建/02-程序/scene_search_controller.py");
	CString strPyWorkDir = exepath_full;
	int nlastSlash = strPyWorkDir.ReverseFind(_T('/'));
	CString pyFileName;
	if (nlastSlash != -1)
	{
		strPyWorkDir = strPyWorkDir.Left(nlastSlash);
		pyFileName = exepath_full.Mid(nlastSlash + 1); 
	}
	else
	{
		pyFileName = exepath_full;
	}
	CString pyece=_T("python");
	CString cmdline;
	cmdline.Format(_T("\"%s\" \"%s\""), pyece, pyFileName);
	RunPythonScript_total(cmdline, strPyWorkDir);
}
UINT __cdecl CPreprocessDoc::RunPyThreadProc1(LPVOID pParam)
{
	CPreprocessDoc* pDoc = (CPreprocessDoc*)pParam;
	if (!pDoc)
	{
		return 0;
	}
	pDoc->Onstartforwardsimulation();
	CString pyece = _T("python");	
	//python forward_simulation_runner.py --q-int 180 --emissivity-ir 0.80 --absorptivity-solar 0.63
	CString exepath_full = _T("coreprogram/01-正向仿真/02-程序/forward_simulation_runner.py");
	CString strPyWorkDir = exepath_full;
	int nlastSlash = strPyWorkDir.ReverseFind(_T('/'));
	CString pyFileName;
	if (nlastSlash != -1)
	{
		strPyWorkDir = strPyWorkDir.Left(nlastSlash);
		pyFileName = exepath_full.Mid(nlastSlash + 1);
	}
	else
	{
		pyFileName = exepath_full;
	}	
	CString cmdline;
	cmdline.Format(_T("\"%s\" \"%s\" \"%s\" %lf \"%s\" %lf \"%s\" %lf"), pyece, pyFileName,_T("--q-int"), pDoc->m_q_int, _T("--emissivity-ir"), pDoc->m_emissity_ir, _T("--absorptivity-solar"), pDoc->m_absorptivity_solar);
	AfxMessageBox(cmdline);	
	STARTUPINFO si = { 0 };
	PROCESS_INFORMATION pi = { 0 };
	si.cb = sizeof(si);
	DWORD dwCreateFlag = 0;
	BOOL bOk = CreateProcess(NULL, cmdline.GetBuffer(), NULL, NULL, FALSE, dwCreateFlag, NULL, strPyWorkDir.GetBuffer(), &si, &pi);
	if (!bOk)
	{
		DWORD err = GetLastError();
		AfxMessageBox(_T("CreateProcess失败，错误码：") + CString(std::to_string(err).c_str()));
		return 0;
	}
	pDoc->m_hPythonProcess1 = pi.hProcess;
	pDoc->m_bIsIsPyRunning1 = TRUE;
	DWORD waitRet;
	while (true)
	{
		waitRet = WaitForSingleObject(pi.hProcess, 100);
		if (waitRet == WAIT_OBJECT_0)
		{
			break;
		}
		if (!pDoc->m_bIsIsPyRunning1)
		{
			TerminateProcess(pi.hProcess, 0);
			break;
		}
	}
	DWORD dwExitCode;
	GetExitCodeProcess(pi.hProcess, &dwExitCode);
	CloseHandle(pi.hThread);
	CloseHandle(pi.hProcess);
	pDoc->m_hPythonProcess1 = NULL;
	pDoc->m_bIsIsPyRunning1 = FALSE;
	POSITION pos = pDoc->GetFirstViewPosition();
	if(pos!=NULL)
	{
		CWnd* pview = pDoc->GetNextView(pos);
		if (pview != nullptr)
		{
			pview->SendMessage(WM_PYTHON_FINISH1, dwExitCode, 0);
		}
	}
	return 0;
}
UINT __cdecl CPreprocessDoc::RunPyThreadProc2(LPVOID pParam)
{
	CPreprocessDoc* pDoc = (CPreprocessDoc*)pParam;
	if (!pDoc)
	{
		return 0;
	}
	pDoc->Onstartsurrogateprediction();
	CString pyece = _T("python");
	CString exepath_full = _T("coreprogram/02-智能预测/02-程序/surrogate_prediction_runner.py");
	CString strPyWorkDir = exepath_full;
	int nlastSlash = strPyWorkDir.ReverseFind(_T('/'));
	CString pyFileName;
	if (nlastSlash != -1)
	{
		strPyWorkDir = strPyWorkDir.Left(nlastSlash);
		pyFileName = exepath_full.Mid(nlastSlash + 1);
	}
	else
	{
		pyFileName = exepath_full;
	}
	//CString parameter_file = _T("request.json");
	CString parameter_file = _T("../01-输入文件/request.json");
	//CString parameter_file = _T("F:/2026Program/Pre-process/Pre-process/coreprogram/02-智能预测/01-输入文件/request.json");
	CString parameter_mode1;
	CString parameter_mode2;
	parameter_mode1 = _T("--mode");
	if (pDoc->m_simulation_mode == 1)
	{
		parameter_mode2 = _T("temperature");
	}
	else if (pDoc->m_simulation_mode == 2)
	{
		parameter_mode2 = _T("point-image");
	}
	else if (pDoc->m_simulation_mode == 0)
	{
		parameter_mode2 = _T("both");
	}

	CString cmdline;
	cmdline.Format(_T("%s %s --params-json %s %s %s"), pyece, pyFileName, parameter_file, parameter_mode1, parameter_mode2);
	//cmdline.Format(_T("cmd /c %s %s --params-json %s %s %s &pause"), pyece, pyFileName, parameter_file, parameter_mode1, parameter_mode2);
	STARTUPINFO si = { 0 };
	PROCESS_INFORMATION pi = { 0 };
	si.cb = sizeof(si);
	DWORD dwCreateFlag = 0;
	BOOL bOk = CreateProcess(NULL, cmdline.GetBuffer(), NULL, NULL, FALSE, dwCreateFlag, NULL, strPyWorkDir.GetBuffer(), &si, &pi);
	//AfxMessageBox(cmdline);
	if (!bOk)
	{
		DWORD err = GetLastError();
		AfxMessageBox(_T("CreateProcess失败，错误码：") + CString(std::to_string(err).c_str()));
		return 0;
	}
	pDoc->m_hPythonProcess2 = pi.hProcess;
	pDoc->m_bIsIsPyRunning2 = TRUE;
	DWORD waitRet;
	while (true)
	{
		waitRet = WaitForSingleObject(pi.hProcess, 100);
		if (waitRet == WAIT_OBJECT_0)
		{
			break;
		}
		if (!pDoc->m_bIsIsPyRunning2)
		{
			TerminateProcess(pi.hProcess, 0);
			break;
		}
	}
	DWORD dwExitCode;
	GetExitCodeProcess(pi.hProcess, &dwExitCode);
	CloseHandle(pi.hThread);
	CloseHandle(pi.hProcess);
	pDoc->m_hPythonProcess2 = NULL;
	pDoc->m_bIsIsPyRunning2 = FALSE;
	POSITION pos = pDoc->GetFirstViewPosition();
	if (pos != NULL)
	{
		CWnd* pview = pDoc->GetNextView(pos);
		if (pview != nullptr)
		{
			pview->SendMessage(WM_PYTHON_FINISH2, dwExitCode, 0);
		}
	}
	return 0;
}
UINT __cdecl CPreprocessDoc::RunPyThreadProc3(LPVOID pParam)
{
	CPreprocessDoc* pDoc = (CPreprocessDoc*)pParam;
	if (!pDoc)
	{
		return 0;
	}
	if (!pDoc->Onstartsimilaritypredict())
	{
		return 0;
	}
	CString pyece = _T("python");
	CString exepath_full = _T("coreprogram/03-相似度评估/02-程序/similarity_evaluator.py");
	CString strPyWorkDir = exepath_full;
	int nlastSlash = strPyWorkDir.ReverseFind(_T('/'));
	CString pyFileName;
	if (nlastSlash != -1)
	{
		strPyWorkDir = strPyWorkDir.Left(nlastSlash);
		pyFileName = exepath_full.Mid(nlastSlash + 1);
	}
	else
	{
		pyFileName = exepath_full;
	}
	CString cmdpre[4];
	cmdpre[0] = _T("--target-csv");
	cmdpre[1] = _T("--candidate-csv");
	cmdpre[2] = _T("--target-id");
	if(pDoc->m_predict_mode==0)
	{
		cmdpre[3] = _T("--candidate-id");
	}
	else
	{
		cmdpre[3] = _T("--sphere-id");
	}
	CString cmdline;
	cmdline.Format(_T("%s %s %s %s %s %s %s %s %s %s"), pyece, pyFileName, cmdpre[0], Utf8CharArrToCString(pDoc->m_target_cscv), cmdpre[1], Utf8CharArrToCString(pDoc->m_candidate_cscv), cmdpre[2], Utf8CharArrToCString(pDoc->m_target_id), cmdpre[3], Utf8CharArrToCString(pDoc->m_candidate_id));
	//cmdline.Format(_T("cmd /c %s %s %s %s %s %s %s %s %s %s &pause"), pyece, pyFileName, cmdpre[0], Utf8CharArrToCString(pDoc->m_target_cscv), cmdpre[1], Utf8CharArrToCString(pDoc->m_candidate_cscv), cmdpre[2], Utf8CharArrToCString(pDoc->m_target_id), cmdpre[3], Utf8CharArrToCString(pDoc->m_candidate_id));
	AfxMessageBox(cmdline);
	STARTUPINFO si = { 0 };
	PROCESS_INFORMATION pi = { 0 };
	si.cb = sizeof(si);
	DWORD dwCreateFlag = 0;
	BOOL bOk = CreateProcess(NULL, cmdline.GetBuffer(), NULL, NULL, FALSE, dwCreateFlag, NULL, strPyWorkDir.GetBuffer(), &si, &pi);
	if (!bOk)
	{
		DWORD err = GetLastError();
		AfxMessageBox(_T("CreateProcess失败，错误码：") + CString(std::to_string(err).c_str()));
		return 0;
	}
	pDoc->m_hPythonProcess3 = pi.hProcess;
	pDoc->m_bIsIsPyRunning3 = TRUE;
	DWORD waitRet;
	while (true)
	{
		waitRet = WaitForSingleObject(pi.hProcess, 100);
		if (waitRet == WAIT_OBJECT_0)
		{
			break;
		}
		if (!pDoc->m_bIsIsPyRunning3)
		{
			TerminateProcess(pi.hProcess, 0);
			break;
		}
	}
	DWORD dwExitCode;
	GetExitCodeProcess(pi.hProcess, &dwExitCode);
	CloseHandle(pi.hThread);
	CloseHandle(pi.hProcess);
	pDoc->m_hPythonProcess3 = NULL;
	pDoc->m_bIsIsPyRunning3 = FALSE;
	POSITION pos = pDoc->GetFirstViewPosition();
	if (pos != NULL)
	{
		CWnd* pview = pDoc->GetNextView(pos);
		if (pview != nullptr)
		{
			pview->SendMessage(WM_PYTHON_FINISH3, dwExitCode, 0);
		}
	}
	return 0;
}
UINT __cdecl CPreprocessDoc::RunPyThreadProc4(LPVOID pParam)
{
	CPreprocessDoc* pDoc = (CPreprocessDoc*)pParam;
	if (!pDoc)
	{
		return 0;
	}
	CString pyece = _T("python");
	CString exepath_full = _T("coreprogram/04-红外场景构建/02-程序/scene_search_controller.py");
	CString strPyWorkDir = exepath_full;
	int nlastSlash = strPyWorkDir.ReverseFind(_T('/'));
	CString pyFileName;
	if (nlastSlash != -1)
	{
		strPyWorkDir = strPyWorkDir.Left(nlastSlash);
		pyFileName = exepath_full.Mid(nlastSlash + 1);
	}
	else
	{
		pyFileName = exepath_full;
	}
	CString cmdline;
	cmdline.Format(_T("\"%s\" \"%s\""), pyece, pyFileName);
	STARTUPINFO si = { 0 };
	PROCESS_INFORMATION pi = { 0 };
	si.cb = sizeof(si);
	DWORD dwCreateFlag = 0;
	BOOL bOk = CreateProcess(NULL, cmdline.GetBuffer(), NULL, NULL, FALSE, dwCreateFlag, NULL, strPyWorkDir.GetBuffer(), &si, &pi);
	if (!bOk)
	{
		DWORD err = GetLastError();
		AfxMessageBox(_T("CreateProcess失败，错误码：") + CString(std::to_string(err).c_str()));
		return 0;
	}
	pDoc->m_hPythonProcess4 = pi.hProcess;
	pDoc->m_bIsIsPyRunning4 = TRUE;
	DWORD waitRet;
	while (true)
	{
		waitRet = WaitForSingleObject(pi.hProcess, 100);
		if (waitRet == WAIT_OBJECT_0)
		{
			break;
		}
		if (!pDoc->m_bIsIsPyRunning4)
		{
			TerminateProcess(pi.hProcess, 0);
			break;
		}
	}
	DWORD dwExitCode;
	GetExitCodeProcess(pi.hProcess, &dwExitCode);
	CloseHandle(pi.hThread);
	CloseHandle(pi.hProcess);
	pDoc->m_hPythonProcess4 = NULL;
	pDoc->m_bIsIsPyRunning4 = FALSE;
	POSITION pos = pDoc->GetFirstViewPosition();
	if (pos != NULL)
	{
		CWnd* pview = pDoc->GetNextView(pos);
		if (pview != nullptr)
		{
			pview->SendMessage(WM_PYTHON_FINISH4, dwExitCode, 0);
		}
	}
	return 0;
}
BOOL CPreprocessDoc::SaveModified()
{
 POSITION pos=GetFirstViewPosition();
 while(pos) { auto view=DYNAMIC_DOWNCAST(CPreprocessView,GetNextView(pos)); if(view && !view->m_taskEditor.Commit()) return FALSE; }
 return CDocument::SaveModified();
}
