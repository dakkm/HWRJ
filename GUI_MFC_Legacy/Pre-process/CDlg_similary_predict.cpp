// CDlg_similary_predict.cpp: 实现文件
//

#include "pch.h"
#include "Pre-process.h"
#include "afxdialogex.h"
#include <fstream>
#include"CFunction.h"
#include "CDlg_similary_predict.h"
IMPLEMENT_DYNAMIC(CDlg_similary_predict, CDialogEx)
CDlg_similary_predict::CDlg_similary_predict(CWnd* pParent /*=nullptr*/)
	: CDialogEx(IDD_Dlg_similarity_predict, pParent)
	, m_String_file1(_T(""))
	, m_string_file2(_T("")), m_mode(0)
{
	m_targetIDs.clear();
	m_candidateIDs.clear();
}
CDlg_similary_predict::~CDlg_similary_predict()
{

}
void CDlg_similary_predict::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
	DDX_Control(pDX, IDC_EDIT_file1, m_edit_file1);
	DDX_Control(pDX, IDC_EDIT_file2, m_edit_file2);
	DDX_Text(pDX, IDC_EDIT_file1, m_String_file1);
	DDX_Text(pDX, IDC_EDIT_file2, m_string_file2);
	DDX_Control(pDX, IDC_COMBO_target_ID, m_comb_targetID);
	DDX_Control(pDX, IDC_COMBO_CandidateID, m_comb_candidateID);
}
BEGIN_MESSAGE_MAP(CDlg_similary_predict, CDialogEx)
	ON_BN_CLICKED(IDC_BUTTON_file1, &CDlg_similary_predict::OnBnClickedButtonfile1)
	ON_BN_CLICKED(IDC_BUTTON_file2, &CDlg_similary_predict::OnBnClickedButtonfile2)
	ON_CBN_SELCHANGE(IDC_COMBO_target_ID, &CDlg_similary_predict::OnCbnSelchangeCombotargetId)
	ON_CBN_SELCHANGE(IDC_COMBO_CandidateID, &CDlg_similary_predict::OnCbnSelchangeComboCandidateid)
END_MESSAGE_MAP()
// CDlg_similary_predict 消息处理程序
void CDlg_similary_predict::OnBnClickedButtonfile1()
{
	TCHAR szFilters[] = _T("文件 (*.*)|*.*|所有类型(*.*)|*.*||");
	CFileDialog fdlg(TRUE, _T(""), _T(""), OFN_FILEMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR, szFilters);
	if (fdlg.DoModal() == IDOK)
	{
		CString stri = fdlg.GetPathName();
		m_String_file1 = stri;
		m_edit_file1.SetWindowTextW(m_String_file1);
		std::ifstream f;
		f.open(m_String_file1);
		m_targetIDs.clear();
		std::unordered_set<std::string> exist;
		std::string line;
		std::getline(f, line);
		while (std::getline(f,line))
		{
			auto fields = splitbycomma(line);
			if (fields.empty())
			{
				continue;
			}
			std::string firstcol = fields[0];
			if (exist.find(firstcol)==exist.end())
			{
				exist.insert(firstcol);
				m_targetIDs.push_back(firstcol);
			}
		}
		f.close();
		m_comb_targetID.ResetContent();
		for (int i = 0; i < (int)m_targetIDs.size(); i++)
		{
			m_comb_targetID.AddString(Utf8CharArrToCString(m_targetIDs[i].c_str()));
		}
		m_comb_targetID.SetCurSel(0);
		m_select_target = 0;
	}
}
void CDlg_similary_predict::OnBnClickedButtonfile2()
{
	TCHAR szFilters[] = _T("文件 (*.*)|*.*|所有类型(*.*)|*.*||");
	CFileDialog fdlg(TRUE, _T(""), _T(""), OFN_FILEMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR, szFilters);
	if (fdlg.DoModal() == IDOK)
	{
		CString stri = fdlg.GetPathName();
		m_string_file2 = stri;
		m_edit_file2.SetWindowTextW(m_string_file2);
		if(m_mode==1)
		{
			std::ifstream f;
			f.open(m_string_file2);
			m_candidateIDs.clear();
			std::unordered_set<std::string> exist;
			std::string line;
			std::getline(f, line);
			while (std::getline(f, line))
			{
				auto fields = splitbycomma(line);
				if (fields.empty())
				{
					continue;
				}
				std::string firstcol = fields[2];
				if (exist.find(firstcol) == exist.end())
				{
					exist.insert(firstcol);
					m_candidateIDs.push_back(firstcol);
				}
			}
			f.close();
		}
		else
		{
			m_candidateIDs.push_back("CH4_C000000");
		}
		m_comb_candidateID.ResetContent();
		for (int i = 0; i < (int)m_candidateIDs.size(); i++)
		{
			m_comb_candidateID.AddString(Utf8CharArrToCString(m_candidateIDs[i].c_str()));
		}
		m_comb_candidateID.SetCurSel(0);
		m_select_candidate = 0;
	}
}
BOOL CDlg_similary_predict::OnInitDialog()
{
	CDialogEx::OnInitDialog();
	m_edit_file1.SetWindowTextW(m_String_file1);
	m_edit_file2.SetWindowTextW(m_string_file2);
	return TRUE;  // return TRUE unless you set the focus to a control
}
void CDlg_similary_predict::OnCbnSelchangeCombotargetId()
{
	m_select_target=m_comb_targetID.GetCurSel();
}
void CDlg_similary_predict::OnCbnSelchangeComboCandidateid()
{
	m_select_candidate = m_comb_candidateID.GetCurSel();
}