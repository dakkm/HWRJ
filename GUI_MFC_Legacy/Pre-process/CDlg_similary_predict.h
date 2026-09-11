#pragma once
#include "afxdialogex.h"
#include<vector>
#include<string>
#include<unordered_set>
// CDlg_similary_predict 对话框

class CDlg_similary_predict : public CDialogEx
{
	DECLARE_DYNAMIC(CDlg_similary_predict)

public:
	CDlg_similary_predict(CWnd* pParent = nullptr);   // 标准构造函数
	virtual ~CDlg_similary_predict();

// 对话框数据
#ifdef AFX_DESIGN_TIME
	enum { IDD = IDD_Dlg_similarity_predict };
#endif

protected:
	virtual void DoDataExchange(CDataExchange* pDX);    // DDX/DDV 支持
	DECLARE_MESSAGE_MAP()
public:
	afx_msg void OnBnClickedButtonfile1();
	afx_msg void OnBnClickedButtonfile2();
	virtual BOOL OnInitDialog();
public:
	CEdit m_edit_file1;
	CEdit m_edit_file2;
	CString m_String_file1;
	CString m_string_file2;
	int m_mode;
	CComboBox m_comb_targetID;
	CComboBox m_comb_candidateID;
public:
	std::vector<std::string> m_targetIDs;
	std::vector<std::string> m_candidateIDs;
	int m_select_target;
	int m_select_candidate;
public:
	afx_msg void OnCbnSelchangeCombotargetId();
	afx_msg void OnCbnSelchangeComboCandidateid();

	


};
