
// Pre-processView.h: CPreprocessView 类的接口
//

#pragma once
#include "TaskEditor.h"


class CPreprocessView : public CView
{
protected: // 仅从序列化创建
	CPreprocessView() noexcept;
	DECLARE_DYNCREATE(CPreprocessView)

// 特性
public:
	CPreprocessDoc* GetDocument() const;

public:
 CTaskEditor m_taskEditor;
 virtual void OnInitialUpdate();
 virtual void OnUpdate(CView*, LPARAM, CObject*);
 afx_msg void OnSize(UINT,int,int);
// 操作
public:

// 重写
public:
	virtual void OnDraw(CDC* pDC);  // 重写以绘制该视图
	virtual BOOL PreCreateWindow(CREATESTRUCT& cs);
protected:

// 实现
public:
	virtual ~CPreprocessView();
#ifdef _DEBUG
	virtual void AssertValid() const;
	virtual void Dump(CDumpContext& dc) const;
#endif

protected:

// 生成的消息映射函数
protected:
	afx_msg void OnFilePrintPreview();
	afx_msg void OnRButtonUp(UINT nFlags, CPoint point);
	afx_msg void OnContextMenu(CWnd* pWnd, CPoint point);
	DECLARE_MESSAGE_MAP()
public:
	afx_msg void Onstartinfraredsituationconstructing();

	afx_msg LRESULT OnPythonFinish1(WPARAM wParam,LPARAM lParam);
	afx_msg LRESULT OnPythonFinish2(WPARAM wParam, LPARAM lParam);
	afx_msg LRESULT OnPythonFinish3(WPARAM wParam, LPARAM lParam);
	afx_msg LRESULT OnPythonFinish4(WPARAM wParam, LPARAM lParam);

	afx_msg void OnstopInfraredconstruction();
	afx_msg void Onstartforwardsimulation();
	afx_msg void Onstartsurrogateprediction();
	afx_msg void Onstartsimilaritypredict();
	afx_msg void Onstopforwardsimulation();
	afx_msg void Onstopsurrogateprediction();
	afx_msg void Onstopsimilaritypredict();
	afx_msg void OnstartsimilaritypredictSphere();
};

#ifndef _DEBUG  // Pre-processView.cpp 中的调试版本
inline CPreprocessDoc* CPreprocessView::GetDocument() const
   { return reinterpret_cast<CPreprocessDoc*>(m_pDocument); }
#endif

