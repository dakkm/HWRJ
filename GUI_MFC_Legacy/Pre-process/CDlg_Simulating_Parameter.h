#pragma once
#include "afxdialogex.h"
// CDlg_Simulating_Parameter 对话框
class CDlg_Simulating_Parameter : public CDialogEx
{
	DECLARE_DYNAMIC(CDlg_Simulating_Parameter)
public:
	CDlg_Simulating_Parameter(CWnd* pParent = nullptr);   // 标准构造函数
	virtual ~CDlg_Simulating_Parameter();
// 对话框数据
#ifdef AFX_DESIGN_TIME
	enum { IDD = IDD_DLG_Simulating_Parameter };
#endif
protected:
	virtual void DoDataExchange(CDataExchange* pDX);    // DDX/DDV 支持
	DECLARE_MESSAGE_MAP()
public:
	// 内部热源功率
	double m_q_int;
	// 红外发射率
	double m_emissivity_ir;
	// 太阳辐射吸收率
	double m_absorptivity_solar;
	int m_simulation_type;
	int m_mode;
	CComboBox m_Combo_mode;
	CStatic m_static_mode;
	virtual BOOL OnInitDialog();
	afx_msg void OnCbnSelchangeCombomode();
};
