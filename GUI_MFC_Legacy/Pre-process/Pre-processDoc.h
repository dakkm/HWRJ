
// Pre-processDoc.h: CPreprocessDoc 类的接口
//
#pragma once
#include "BusinessTask.h"
#define WM_PYTHON_FINISH4 WM_USER+1004
#define WM_PYTHON_FINISH3 WM_USER+1003
#define WM_PYTHON_FINISH2 WM_USER+1002
#define WM_PYTHON_FINISH1 WM_USER+1001
class CPreprocessDoc : public CDocument
{
protected: // 仅从序列化创建
	CPreprocessDoc() noexcept;
	DECLARE_DYNCREATE(CPreprocessDoc)
// 特性
public:
	BusinessTask m_task;
 virtual BOOL OnSaveDocument(LPCTSTR path);
 virtual BOOL SaveModified();
	double m_q_int;
	double m_emissity_ir;
	double m_absorptivity_solar;
	int m_simulation_type;
	int m_simulation_mode;
public:
	int m_predict_mode;
	char m_target_cscv[128];
	char m_candidate_cscv[128];
	char m_target_id[128];
	char m_candidate_id[128];			
// 操作
public:

// 重写
public:
	virtual BOOL OnNewDocument();
	virtual void Serialize(CArchive& ar);
#ifdef SHARED_HANDLERS
	virtual void InitializeSearchContent();
	virtual void OnDrawThumbnail(CDC& dc, LPRECT lprcBounds);
#endif // SHARED_HANDLERS

// 实现
public:
	virtual ~CPreprocessDoc();
#ifdef _DEBUG
	virtual void AssertValid() const;
	virtual void Dump(CDumpContext& dc) const;
#endif

protected:

// 生成的消息映射函数
protected:
	DECLARE_MESSAGE_MAP()

#ifdef SHARED_HANDLERS
	// 用于为搜索处理程序设置搜索内容的 Helper 函数
	void SetSearchContent(const CString& value);
#endif // SHARED_HANDLERS
public:
	void OnSimulatingParameter();
	void Onstartforwardsimulation();
	void Onstartsurrogateprediction();
	bool Onstartsimilaritypredict();
	void Onstartinfraredsituationconstructing();
public:
	HANDLE m_hPythonProcess1;
	BOOL m_bIsIsPyRunning1;
	HANDLE m_hPythonProcess2;
	BOOL m_bIsIsPyRunning2;
	HANDLE m_hPythonProcess3;
	BOOL m_bIsIsPyRunning3;
	HANDLE m_hPythonProcess4;
	BOOL m_bIsIsPyRunning4;
	static UINT __cdecl RunPyThreadProc1(LPVOID pParam);
	static UINT __cdecl RunPyThreadProc2(LPVOID pParam);
	static UINT __cdecl RunPyThreadProc3(LPVOID pParam);
	static UINT __cdecl RunPyThreadProc4(LPVOID pParam);
};
