#pragma once
#include <afxpropertygridctrl.h>
#include "BusinessTask.h"
#include <functional>
#include <vector>
class CTaskEditor : public CWnd
{
public:
    BOOL Create(CWnd* parent, BusinessTask* task);
    void Reload();
    void SelectModule(int module);
    bool Commit();
private:
    BusinessTask* m_task=nullptr;
    CTreeCtrl m_tree;
    CMFCPropertyGridCtrl m_pages[5];
    CStatic m_title, m_note;
    CButton m_files[4];
    int m_page=0;
    int m_selectedTarget=1;
    struct Binding { CMFCPropertyGridProperty* property; std::function<void(const COleVariant&)> set; };
    std::vector<Binding> m_bindings;
    void BuildPages();
    void Layout();
    void ShowPage(int page);
    CMFCPropertyGridProperty* Group(int page, const wchar_t* name, bool expanded=true);
    void Number(CMFCPropertyGridProperty* group,const wchar_t* name,double& value);
    void Integer(CMFCPropertyGridProperty* group,const wchar_t* name,int& value);
    void Text(CMFCPropertyGridProperty* group,const wchar_t* name,CString& value,const wchar_t* choices=nullptr);
    void Boolean(CMFCPropertyGridProperty* group,const wchar_t* name,bool& value);
    afx_msg void OnSize(UINT,int,int);
    afx_msg void OnSelection(NMHDR*,LRESULT*);
    afx_msg LRESULT OnChanged(WPARAM,LPARAM);
    afx_msg LRESULT OnRefresh(WPARAM,LPARAM);
    afx_msg void OnFileButton(UINT id);
    DECLARE_MESSAGE_MAP()
};
