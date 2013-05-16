﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Bjd.option;
using System.Windows.Forms;
using Bjd.util;

namespace Bjd.ctrl{
    public class CtrlDat : OneCtrl{
        private GroupBox _border;
        private List<Button> _buttonList;
        private CheckedListBox _checkedListBox;
        private readonly ListVal _listVal;

        private readonly int _height;
        //private Kernel kernel;
        private readonly bool _isJp;
        private const int Add = 0;
        private const int Edit = 1;
        private const int Del = 2;
        private const int Import = 3;
        public const int Export = 4;
        private const int CLEAR = 5;
        private readonly string[] _tagList = new[]{"Add", "Edit", "Del", "Import", "Export", "Clear"};
        private readonly string[] _strList = new[]{"追加", "変更", "削除", "インポート", "エクスポート", "クリア"};

        public CtrlDat(string help, ListVal listVal, int height, bool isJp) : base(help){
            _listVal = listVal;
            _height = height;
            _isJp = isJp;
        }

        public CtrlType[] CtrlTypeList{
            get{
                var ctrlTypeList = new CtrlType[_listVal.Count];
                int i = 0;
                foreach (var o in _listVal){
                    ctrlTypeList[i++] = o.OneCtrl.GetCtrlType();
                }
                return ctrlTypeList;
            }
        }

        //OnePage(CtrlTabPage.pageList) CtrlGroup CtrlDatにのみ存在する
        public ListVal ListVal{
            get { return _listVal; }
        }

        public override CtrlType GetCtrlType(){
            return CtrlType.Dat;
        }


        protected override void AbstractCreate(object value, ref int tabIndex){
            var left = Margin;
            var top = Margin;

            // ボーダライン（groupPanel）の生成
            _border = (GroupBox) Create(Panel, new GroupBox(), left, top, -1);
            _border.Width = OptionDlg.Width() - 15;
            _border.AutoSize = false;
            _border.Height = _height;
            _border.Text = Help;

            //border = (JPanel) create(Panel, new JPanel(new GridLayout()), left, top);
            //border.setBorder(BorderFactory.createTitledBorder(getHelp()));
            //border.setSize(getDlgWidth() - 32, height); // サイズは、コンストラクタで指定されている

            //Datに含まれるコントロールを配置

            //ボーダーの中でのオフセット移動
            left += 8;
            top += 12;
            _listVal.CreateCtrl(_border, left, top, ref tabIndex);
            _listVal.OnChange += ListValOnChange;
            //listVal.SetListener(this); //コントロール変化のイベントをこのクラスで受信してボタンの初期化に利用する

            //オフセット移動
            var dimension = _listVal.Size;
            top += dimension.Height;

            //ボタンの生成s
            _buttonList = new List<Button>();
            for (int i = 0; i < _tagList.Count(); i++){
                var b = (Button) Create(_border, new Button(), left + 85*i, top, tabIndex++);
                b.Width = 80;
                b.Height = 24;
                b.Tag = i; //インデックス
                b.Text = (_isJp) ? _strList[i] : _tagList[i];
                b.Click += ButtonClick;
                b.Tag = _tagList[i]; //[C#]
                _buttonList.Add(b);
            }


            //オフセット移動
            top += _buttonList[0].Height + Margin;

            //チェックリストボックス配置
            _checkedListBox = (CheckedListBox) Create(_border, new CheckedListBox(), left, top, tabIndex++);
            _checkedListBox.AutoSize = false;
            _checkedListBox.Width = OptionDlg.Width() - 35; // 52;
            _checkedListBox.Height = _height - top -3; // 15;
            _checkedListBox.SelectedIndexChanged += CheckedListBoxSelectedIndexChanged;

            //checkListBox = (CheckListBox) Create(border, new CheckListBox(), left, top,tabIndex++);
            //_checkedListBox.setSize(getDlgWidth() - 52, height - top - 15);
            //		_checkedListBox.addListSelectionListener(this);
            //_checkedListBox.addActionListener(this);

            //値の設定
            AbstractWrite(value);

            // パネルのサイズ設定
            //Panel.Size = new Size(_border.Width + Margin*2, _border.Height + Margin*2);
            Panel.Size = new Size(_border.Width + Margin*2, _border.Height);

            ListValOnChange(); //ボタン状態の初期化
        }

        //コントロールの入力内容に変化があった場合
        public virtual void ListValOnChange(){

            ButtonsInitialise(); //ボタン状態の初期化

        }

        //ボタン状態の初期化
        private void ButtonsInitialise(){
            //コントロールの入力が完了しているか
            //bool isComplete = listVal.isComplete();
            bool isComplete = IsComplete();
            //チェックリストボックスのデータ件数
            int count = _checkedListBox.Items.Count;
            //チェックリストボックスの選択行
            int index = _checkedListBox.SelectedIndex;

            _buttonList[Add].Enabled = isComplete;
            _buttonList[Export].Enabled = (count > 0);
            _buttonList[CLEAR].Enabled = (count > 0);
            _buttonList[Del].Enabled = (index >= 0);
            _buttonList[Edit].Enabled = (index >= 0 && isComplete);
        }


        //ボタンのイベント
        private void ButtonClick(object sender, EventArgs e){
            var cmd = (string) ((Button) sender).Tag;

            var selectedIndex = _checkedListBox.SelectedIndex; // 選択行

            if (cmd == _tagList[Add]){
                //コントロールの内容をテキストに変更したもの
                var s = ControlToText();
                if (s == ""){
                    return;
                }
                //同一のデータがあるかどうかを確認する
                if (_checkedListBox.Items.IndexOf(s) != -1){
                    Msg.Show(MsgKind.Error, _isJp ? "既に同一内容のデータが存在します。" : "There is already the same data");
                    return;
                }
                //チェックリストボックスへの追加
                int index = _checkedListBox.Items.Add(s);
                _checkedListBox.SetItemChecked(index, true); //最初にチェック（有効）状態にする
                _checkedListBox.SelectedIndex = index; //選択状態にする
            }
            else if (cmd == _tagList[Edit]){
                //コントロールの内容をテキストに変更したもの
                string str = ControlToText();
                if (str == ""){
                    return;
                }
                if (str == (string) _checkedListBox.Items[selectedIndex]){
                    Msg.Show(MsgKind.Error, _isJp ? "変更内容はありません" : "There is not a change");
                    return;
                }
                //同一のデータがあるかどうかを確認する
                if (_checkedListBox.Items.IndexOf(str) != -1){
                    Msg.Show(MsgKind.Error, _isJp ? "既に同一内容のデータが存在します" : "There is already the same data");
                    return;
                }
                _checkedListBox.Items[selectedIndex] = str;

            }
            else if (cmd == _tagList[Del]){
                foreach (var v in _listVal){
                    //コントロールの内容をクリア
                    v.OneCtrl.Clear();
                }
                if (selectedIndex >= 0){
                    _checkedListBox.Items.RemoveAt(selectedIndex);
                }
            }
            else if (cmd == _tagList[Import]){
                var d = new OpenFileDialog();
                if (DialogResult.OK == d.ShowDialog()){
                    var lines = File.ReadAllLines(d.FileName);
                    ImportDat(lines.ToList());
                }
//                    catch (IOException e){
//                        Msg.Show(MsgKind.Error, string.format("ファイルの読み込みに失敗しました[%s]", file.getPath()));
//                    }
            }
            else if (cmd == _tagList[Export]){
                var dlg = new SaveFileDialog();
                if (DialogResult.OK == dlg.ShowDialog()){
                    var isExecute = true;
                    if (File.Exists(dlg.FileName)){
                        if (DialogResult.OK != Msg.Show(MsgKind.Question, _isJp ? "上書きして宜しいですか?" : "May I overwrite?")){
                            isExecute = false; //キャンセル
                        }
                    }
                    if (isExecute){
                        var lines = ExportDat();
                        File.WriteAllLines(dlg.FileName,lines.ToArray());
                    }
                }
            }
            else if (cmd == _tagList[CLEAR]){
                if (DialogResult.OK ==
                    Msg.Show(MsgKind.Question, _isJp ? "すべてのデータを削除してよろしいですか" : "May I eliminate all data?")){
                    _checkedListBox.Items.Clear();
                }
                foreach (OneVal v in _listVal){
                    //コントロールの内容をクリア
                    v.OneCtrl.Clear();
                }
            }
        }

        //チェックボックス用のテキストを入力コントロールに戻す
        private void TextToControl(string str){
            var tmp = str.Split('\t');
            if (_listVal.Count != tmp.Length){
                Msg.Show(MsgKind.Error, (_isJp) ? "項目数が一致しません" : "The number of column does not agree");
                return;
            }
            var i = 0;
            foreach (var v in _listVal){
                v.OneCtrl.FromText(tmp[i++]);
            }
        }

        //入力コントロールの内容をチェックボックス用のテキストに変換する
        private string ControlToText(){

            var sb = new StringBuilder();
            foreach (var v in _listVal){
                if (sb.Length != 0){
                    sb.Append("\t");
                }
                sb.Append(v.OneCtrl.ToText());
            }
            return sb.ToString();

        }

        //インポート
        private void ImportDat(List<string> lines){
            foreach (var s in lines){
                string str = s;
                bool isChecked = str[0] != '#';
                str = str.Substring(2);

                //カラム数の確認
                string[] tmp = str.Split('\t');
                if (_listVal.Count != tmp.Length){
                    Msg.Show(MsgKind.Error,
                             string.Format("{0} [ {1} ] ",
                                           _isJp
                                               ? "カラム数が一致しません。この行はインポートできません。"
                                               : "The number of column does not agree and cannot import this line.", str));
                    continue;
                }
                //Ver5.0.0-a9 パスワード等で暗号化されていない（平文の）場合は、ここで
                bool isChange = false;
                if (isChange){
                    var sb = new StringBuilder();
                    foreach (string l in tmp){
                        if (sb.Length != 0){
                            sb.Append('\t');
                        }
                        sb.Append(l);
                    }
                    str = sb.ToString();
                }
                //同一のデータがあるかどうかを確認する
                if (_checkedListBox.Items.IndexOf(str) != -1){
                    Msg.Show(MsgKind.Error,
                             string.Format("{0} [ {1} ] ",
                                           _isJp
                                               ? "データ重複があります。この行はインポートできません。"
                                               : "There is data repetition and cannot import this line.", str));
                    continue;
                }

                int index = _checkedListBox.Items.Add(str);
                //最初にチェック（有効）状態にする
                _checkedListBox.SetItemChecked(index, isChecked);
                _checkedListBox.SelectedIndex = index;
            }
        }

        //エクスポート
        private List<string> ExportDat(){
            //チェックリストボックスの内容からDatオブジェクトを生成する
            var lines = new List<String>();
            for (int i = 0; i < _checkedListBox.Items.Count; i++){
                var s = (string) _checkedListBox.Items[i];
                lines.Add(_checkedListBox.GetItemCheckState(i) == CheckState.Checked ? string.Format(" \t{0}", s) : string.Format("#\t{0}", s));
            }
            return lines;
        }

        void CheckedListBoxSelectedIndexChanged(object sender, EventArgs e){
            int index = _checkedListBox.SelectedIndex;
             ButtonsInitialise(); //ボタン状態の初期化
             //チェックリストの内容をコントロールに転送する
            if (index >= 0) {
                TextToControl((String)_checkedListBox.Items[index]);
            }
//            } else {
//                this.SetOnChange();
//            }
        }

        protected override void AbstractDelete(){
	        _listVal.DeleteCtrl(); //これが無いと、グループの中のコントロールが２回目以降表示されなくなる

	        if (_buttonList != null){
	            for (var i = 0; i < _buttonList.Count; i++){
	                Remove(_border, _buttonList[i]);
	                _buttonList[i] = null;
	            }
	        }
	        Remove(Panel, _border);
	        Remove(Panel, _checkedListBox);
	        _border = null;
	    }

	    //コントロールの入力が完了しているか
        protected new virtual bool IsComplete() {
	    	return _listVal.IsComplete();
    	}

        //***********************************************************************
	    // コントロールの値の読み書き
	    //***********************************************************************
        protected override object AbstractRead(){
            var dat = new Dat(CtrlTypeList);
            //チェックリストボックスの内容からDatオブジェクトを生成する
            for (int i = 0; i < _checkedListBox.Items.Count; i++){
                bool enable = _checkedListBox.GetItemChecked(i);
                if (!dat.Add(enable, _checkedListBox.Items[i].ToString())) {
                    Util.RuntimeException("CtrlDat abstractRead() 外部入力からの初期化ではないので、このエラーは発生しないはず");
                }
            }
            return dat;
        }

        protected override void AbstractWrite(object value){
            if (value == null){
                return;
            }
            var dat = (Dat) value;
            foreach (var d in dat){
                var sb = new StringBuilder();
                //List<string> strList = d.StrList;
                foreach (var s in d.StrList) {
                    if (sb.Length != 0){
                        sb.Append("\t");
                    }
                    sb.Append(s);
                }
                int i = _checkedListBox.Items.Add(sb.ToString());
                _checkedListBox.SetItemChecked(i, d.Enable);
            }
            //データがある場合は、１行目を選択する
            if (_checkedListBox.Items.Count > 0) {
                _checkedListBox.SelectedItem = 0;
            }
        }

    	//***********************************************************************
    	// コントロールへの有効・無効
	    //***********************************************************************

        protected override void AbstractSetEnable(bool enabled){
            if (_border != null){
                //CtrlDatの場合は、disableで非表示にする
                Panel.Enabled = enabled;
                //border.setEnabled(enabled);
            }
        }
    
	    //***********************************************************************
	    // OnChange関連
	    //***********************************************************************
	    // 必要なし
	    //***********************************************************************
	    // CtrlDat関連
	    //***********************************************************************
        protected override bool AbstractIsComplete(){
    		Util.RuntimeException("使用禁止");
            return false;
        }

        protected override string AbstractToText(){
    		Util.RuntimeException("使用禁止");
            return null;
        }

        protected override void AbstractFromText(string s){
    		Util.RuntimeException("使用禁止");
        }

        protected override void AbstractClear(){
    		Util.RuntimeException("使用禁止");
        }
    }

}
/*
	@Override
	public  void actionPerformed(ActionEvent e) {

		string cmd = e.getActionCommand();
		string source = e.getSource().getClass().getName();

		if (source.indexOf("JButton") != -1) {
			actionButton(cmd); //ボタンのイベント
		} else if (source.indexOf("CheckListBox") != -1) {
			actionCheckListBox(cmd); //チェックリストボックスのイベント
		}

	}

	//チェックリストボックスのイベント
	 void actionCheckListBox(string cmd) {
	}

}
*/