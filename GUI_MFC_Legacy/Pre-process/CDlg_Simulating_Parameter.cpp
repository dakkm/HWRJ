// CDlg_Simulating_Parameter.cpp: 实现文件
//

#include "pch.h"
#include "Pre-process.h"
#include "afxdialogex.h"
#include "CDlg_Simulating_Parameter.h"
// CDlg_Simulating_Parameter 对话框
IMPLEMENT_DYNAMIC(CDlg_Simulating_Parameter, CDialogEx)
CDlg_Simulating_Parameter::CDlg_Simulating_Parameter(CWnd* pParent /*=nullptr*/)
	: CDialogEx(IDD_DLG_Simulating_Parameter, pParent)
	, m_q_int(300)
	, m_emissivity_ir(0.95)
	, m_absorptivity_solar(0.95)
{
	m_simulation_type=0;
	m_mode=0;
}
CDlg_Simulating_Parameter::~CDlg_Simulating_Parameter()
{
}
void CDlg_Simulating_Parameter::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
	DDX_Text(pDX, IDC_EDIT_q_int, m_q_int);
	DDV_MinMaxDouble(pDX, m_q_int, 0, 300);
	DDX_Text(pDX, IDC_EDIT_emissivity_ir, m_emissivity_ir);
	DDV_MinMaxDouble(pDX, m_emissivity_ir, 0.2, 0.95);
	DDX_Text(pDX, IDC_EDIT_absorptivity_solar, m_absorptivity_solar);
	DDV_MinMaxDouble(pDX, m_absorptivity_solar, 0.2, 0.95);
	DDX_Control(pDX, IDC_COMBO_mode, m_Combo_mode);
	DDX_Control(pDX, IDC_STATIC_Mode, m_static_mode);
}
BEGIN_MESSAGE_MAP(CDlg_Simulating_Parameter, CDialogEx)
	ON_CBN_SELCHANGE(IDC_COMBO_mode, &CDlg_Simulating_Parameter::OnCbnSelchangeCombomode)
END_MESSAGE_MAP()
BOOL CDlg_Simulating_Parameter::OnInitDialog()
{
	CDialogEx::OnInitDialog();
	if (m_simulation_type == 0)
	{
		GetDlgItem(IDC_COMBO_mode)->ShowWindow(SW_HIDE);
		GetDlgItem(IDC_STATIC_Mode)->ShowWindow(SW_HIDE);
	}
	else
	{
		GetDlgItem(IDC_COMBO_mode)->ShowWindow(SW_SHOW);
		GetDlgItem(IDC_STATIC_Mode)->ShowWindow(SW_SHOW);
	}
	m_Combo_mode.AddString(L"温度代理和点图像Transformer都执行");
	m_Combo_mode.AddString(L"仅温度代理");
	m_Combo_mode.AddString(L"仅点图像Transformer");	
	m_Combo_mode.SetCurSel(m_mode);
	return TRUE;  
}
void CDlg_Simulating_Parameter::OnCbnSelchangeCombomode()
{
	m_mode = m_Combo_mode.GetCurSel();
}
