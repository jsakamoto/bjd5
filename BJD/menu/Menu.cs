﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Bjd.menu{

    //メニューを管理するクラス
    public class Menu : IDisposable{
        private readonly Kernel _kernel;
        private readonly MenuStrip _menuStrip;
        private readonly System.Timers.Timer _timer; //[C#]
        private readonly Dictionary<OneMenu, ToolStripMenuItem> _ar = new Dictionary<OneMenu, ToolStripMenuItem>();

        //Java fix
        private bool _isJp;


        //[C#]【メニュー選択時のイベント】
        //public delegate void MenuClickHandler(ToolStripMenuItem menu);//デリゲート
        //public event MenuClickHandler OnClick;//イベント
        readonly Queue<ToolStripMenuItem> _queue = new Queue<ToolStripMenuItem>();

        public Menu(Kernel kernel, MenuStrip menuStrip){
            _kernel = kernel;
            _menuStrip = menuStrip;
            
            //[C#]
            _timer = new System.Timers.Timer{Interval = 100};
            _timer.Elapsed += TimerElapsed;
            _timer.Enabled = true;
        }

        //[C#]メニュー選択のイベントを発生させる
        //synchro=false 非同期で動作する
        public void EnqueueMenu(string name, bool synchro) {
            var item = new ToolStripMenuItem { Name = name };
            if (synchro) {
                _kernel.MenuOnClick(name);
            } else {
                _queue.Enqueue(item);//キューに格納する
            }
        }

        //[C#]タイマー起動でキューに入っているメニューイベントを実行する
        void TimerElapsed(object sender, System.Timers.ElapsedEventArgs e) {
            if (_queue.Count > 0) {
                var q = _queue.Dequeue();
                _kernel.MenuOnClick(q.Name);
            }
        }

        //終了処理
        public void Dispose(){
            //while (menuBar.getMenuCount() > 0){
            //    JMenu m = menuBar.getMenu(0);
            //    m.removeAll();
            //    menuBar.remove(m);
            //}
            //menuBar.invalidate();
        }

        //Java fix パラメータisJpを追加
        //メニュー構築（内部テーブルの初期化）
        public void InitializeRemote(bool isJp) {
            if (_menuStrip == null)
                return;
            
            if (_menuStrip.InvokeRequired) {
                _menuStrip.BeginInvoke(new MethodInvoker(()=>InitializeRemote(isJp)));
            } else {
                //Java fix
                _isJp = isJp;
                //全削除
                _menuStrip.Items.Clear();
                _ar.Clear();

                var subMenu = new ListMenu { new OneMenu("File_Exit", "終了", "Exit",'X',Keys.None) };
                //「ファイル」メニュー
                var m = AddSubMenu(_menuStrip.Items, new OneMenu("File", "ファイル", "File", 'F', Keys.None));
                AddListMenu(m, subMenu);

            }
        }

        //メニュー構築（内部テーブルの初期化） リモート用
//        public void initializeRemote(){
//            if (menuBar == null){
//                return;
//            }
//
//            //全削除
//            menuBar.removeAll();
//
//            ListMenu subMenu = new ListMenu();
//            subMenu.add(new OneMenu("File_Exit", "終了", "Exit", 'X', null));
//
//            //「ファイル」メニュー
//            JMenu m = addTopMenu(new OneMenu("File", "ファイル", "File", 'F', null));
//            addListMenu(m, subMenu);
//        }



        //Java fix パラメータisJpを追加
        //メニュー構築（内部テーブルの初期化） 通常用
        public void Initialize(bool isJp){
            _isJp = isJp;

            if (_menuStrip == null){
                return;
            }

            if (_menuStrip.InvokeRequired){
                _menuStrip.Invoke(new MethodInvoker(()=>Initialize(isJp)));
            }else{
                //全削除
                _menuStrip.Items.Clear();
                _ar.Clear();

                //「ファイル」メニュー
                var m = AddSubMenu(_menuStrip.Items, new OneMenu("File", "ファイル","File", 'F', Keys.None));
                AddListMenu(m, FileMenu());


                //「オプション」メニュー
                m = AddSubMenu(_menuStrip.Items, new OneMenu("Option", "オプション", "Option", 'O', Keys.None));
                AddListMenu(m, _kernel.ListOption.GetListMenu());

                //「ツール」メニュー
                m = AddSubMenu(_menuStrip.Items, new OneMenu("Tool", "ツール", "Tool", 'T', Keys.None));
                AddListMenu(m, _kernel.ListTool.GetListMenu());
                //
                //「起動/停止」メニュー
                m = AddSubMenu(_menuStrip.Items, new OneMenu("StartStop", "起動/停止", "Start/Stop", 'S', Keys.None));
                AddListMenu(m, StartStopMenu());
                
                //「ヘルプ」メニュー
                m = AddSubMenu(_menuStrip.Items, new OneMenu("Help", "ヘルプ", "Help", 'H', Keys.None));
                AddListMenu(m, HelpMenu());

                //Java fix
                SetEnable();//状況に応じた有効無効
                // menuBar.updateUI(); //メニューバーの再描画
            }
        }

        //ListMenuの追加 (再帰)
//        private void AddListMenu(JMenu owner, ListMenu subMenu){
//            foreach (OneMenu o in subMenu){
//                addSubMenu(owner, o);
//            }
//        }

//        //OneMenuの追加
//        private void AddSubMenu(JMenu owner, OneMenu oneMenu){
//
//            if (oneMenu.getName().equals("-")){
//                owner.addSeparator();
//                return;
//            }
//            if (oneMenu.getSubMenu().size() != 0){
//                JMenu m = createMenu(oneMenu);
//                addListMenu(m, oneMenu.getSubMenu()); //再帰処理
//                owner.add(m);
//            }
//            else{
//                JMenuItem menuItem = createMenuItem(oneMenu);
//                JMenuItem item = (JMenuItem) owner.add(menuItem);
//                ar.add(item);
//            }
//        }

        //ListMenuの追加 (再帰)
        void AddListMenu(ToolStripMenuItem owner, ListMenu subMenu) {
            foreach (var o in subMenu) {
                AddSubMenu(owner.DropDownItems, o);
            }
        }
        //OneMenuの追加
        ToolStripMenuItem AddSubMenu(ToolStripItemCollection items, OneMenu o) {
            if (o.Name == "-") {
                items.Add("-");//ToolStripSeparatorが生成される
                return null;
            }

            //Java fix _isJp対応
            var title = string.Format("{0}", o.EnTitle);
            if (_isJp){
                title = string.Format("{0}(&{1})", o.JpTitle, o.Mnemonic);
                if (o.Mnemonic == '0') { //0が指定された場合、ショートカットは無効
                    title = o.JpTitle;
                }
            }
            var item = (ToolStripMenuItem)items.Add(title);
            

            item.Name = o.Name;//名前
            item.ShortcutKeys = o.Accelerator;//ショッートカット
            item.Click += MenuItemClick;//クリックイベンント
            AddListMenu(item, o.SubMenu);//再帰処理(o.SubMenu.Count==0の時、処理なしで戻ってくる)

            _ar.Add(o,item);//内部テーブルへの追加
            return item;
        }


//        private JMenu CreateMenu(OneMenu oneMenu){
//            JMenu m = new JMenu(oneMenu.getTitle(kernel.isJp()));
//            m.setActionCommand(oneMenu.getName());
//            m.setMnemonic(oneMenu.getMnemonic());
//            //		JMenuにはアクセラレータを設定できない
//            //		if (oneMenu.getStrAccelerator() != null) {
//            //			m.setAccelerator(KeyStroke.getKeyStroke(oneMenu.getStrAccelerator()));
//            //		}
//            m.addActionListener(this);
//            m.setName(oneMenu.getTitle(kernel.isJp()));
//            return m;
//        }
//
//        private JMenuItem CreateMenuItem(OneMenu oneMenu){
//            JMenuItem menuItem = new JMenuItem(oneMenu.getTitle(kernel.isJp()));
//            menuItem.setActionCommand(oneMenu.getName());
//            menuItem.setMnemonic(oneMenu.getMnemonic());
//            if (oneMenu.getStrAccelerator() != null){
//                menuItem.setAccelerator(KeyStroke.getKeyStroke(oneMenu.getStrAccelerator()));
//            }
//            menuItem.addActionListener(this);
//            menuItem.setName(oneMenu.getTitle(kernel.isJp()));
//            return menuItem;
//        }
//
//        //メニューバーへの追加
//        private JMenu AddTopMenu(OneMenu oneMenu){
//            JMenu menu = new JMenu(oneMenu.getTitle(kernel.isJp()));
//            menu.setMnemonic(oneMenu.getMnemonic());
//            menuBar.add(menu);
//            return menu;
//        }

        //メニュー選択時のイベント処理
//        public void actionPerformed(ActionEvent e){
//            kernel.menuOnClick(e.getActionCommand());
//        }
        //メニュー選択時のイベント処理
        void MenuItemClick(object sender, EventArgs e){

            var cmd = ((ToolStripMenuItem) sender).Name;
            _kernel.MenuOnClick(cmd);
//            if (OnClick != null) {
//                OnClick((ToolStripMenuItem)sender);
//            }
        }



        //状況に応じた有効/無効のセット
        public void SetEnable(){
            if (_kernel.RunMode == RunMode.NormalRegist){
                //サービス登録されている場合
                //サーバの起動停止はできない
                SetEnabled("StartStop_Start", false);
                SetEnabled("StartStop_Stop", false);
                SetEnabled("StartStop_Restart", false);
                SetEnabled("StartStop_Service", true);
                SetEnabled("File_LogClear", false);
                SetEnabled("File_LogCopy", false);
                SetEnabled("File_Trace", false);
                SetEnabled("Tool", false);
            }
            else if (_kernel.RunMode == RunMode.Remote){
                //リモートクライアント
                //サーバの再起動のみ
                SetEnabled("StartStop_Start", false);
                SetEnabled("StartStop_Stop", false);
                SetEnabled("StartStop_Restart", true);
                SetEnabled("StartStop_Service", false);
                SetEnabled("File_LogClear", true);
                SetEnabled("File_LogCopy", true);
                SetEnabled("File_Trace", true);
                SetEnabled("Tool", true);
            }
            else{
                //通常起動
                //Util.sleep(0); //起動・停止が全部完了してから状態を取得するため
                var isRunning = _kernel.ListServer.IsRunnig();
                SetEnabled("StartStop_Start", !isRunning);
                SetEnabled("StartStop_Stop", isRunning);
                SetEnabled("StartStop_Restart", isRunning);
                SetEnabled("StartStop_Service", !isRunning);
                SetEnabled("File_LogClear", true);
                SetEnabled("File_LogCopy", true);
                SetEnabled("File_Trace", true);
                SetEnabled("Tool", true);
            }
        }

        //有効/無効
        private void SetEnabled(String name, bool enabled){
//		foreach (JMenuItem m in ar) {
//			String s = m.getActionCommand(); //ActionCommandは、OneMenuのnameで初期化されている
//			if (s == name) {
//				m.SetEnabled(enabled);
//			}
//		}
            foreach (var o in _ar){
                if (o.Key.Name == name){
                    o.Value.Enabled = enabled;
                    return;
                }
            }
        }

        //「ファイル」のサブメニュー
        private ListMenu FileMenu(){
            ListMenu subMenu = new ListMenu();
            subMenu.Add(new OneMenu("File_LogClear", "ログクリア", "Loglear", 'C', Keys.F1));
            subMenu.Add(new OneMenu("File_LogCopy", "ログコピー", "LogCopy", 'L', Keys.F2));
            subMenu.Add(new OneMenu("File_Trace", "トレース表示", "Trace", 'T', Keys.None));
            subMenu.Add(new OneMenu()); // セパレータ
            subMenu.Add(new OneMenu("File_Exit", "終了", "Exit", 'X', Keys.None));
            return subMenu;
        }

        //「起動/停止」のサブメニュー
        private ListMenu StartStopMenu(){
            ListMenu subMenu = new ListMenu();
            subMenu.Add(new OneMenu("StartStop_Start", "サーバ起動", "Start", 'S', Keys.None));
            subMenu.Add(new OneMenu("StartStop_Stop", "サーバ停止", "Stop", 'P', Keys.None));
            subMenu.Add(new OneMenu("StartStop_Restart", "サーバ再起動", "Restart", 'R', Keys.None));
            subMenu.Add(new OneMenu("StartStop_Service", "サービス設定", "Service", 'S', Keys.None));
            return subMenu;
        }

        //「ヘルプ」のサブメニュー
        private ListMenu HelpMenu(){
            ListMenu subMenu = new ListMenu();
            subMenu.Add(new OneMenu("Help_Homepage", "ホームページ", "HomePage", 'H', Keys.None));
            subMenu.Add(new OneMenu("Help_Document", "ドキュメント", "Document", 'D', Keys.None));
            subMenu.Add(new OneMenu("Help_Support", "サポート掲示板", "Support", 'S', Keys.None));
            subMenu.Add(new OneMenu("Help_Version", "バージョン情報", "Version", 'V', Keys.None));
            return subMenu;
        }
    }
}

/*
    public class Menu {
        readonly Kernel _kernel;
        readonly MenuStrip _menuStrip;


        readonly System.Timers.Timer _timer;
        readonly Queue<ToolStripMenuItem> _queue = new Queue<ToolStripMenuItem>();


        readonly Dictionary<OneMenu, ToolStripMenuItem> _ar = new Dictionary<OneMenu, ToolStripMenuItem>();

        //【メニュー選択時のイベント】
        public delegate void MenuClickHandler(ToolStripMenuItem menu);//デリゲート
        public event MenuClickHandler OnClick;//イベント

        public Menu(Kernel kernel,MenuStrip menuStrip) {
            _kernel = kernel;
            _menuStrip = menuStrip;

            _timer = new System.Timers.Timer{Interval = 100};
            _timer.Elapsed+=TimerElapsed;
            _timer.Enabled = true;
        }



        //メニュー構築（内部テーブルの初期化）
        public void InitializeRemote() {
            if (_menuStrip == null)
                return;

            if (_menuStrip.InvokeRequired) {
                _menuStrip.BeginInvoke(new MethodInvoker(InitializeRemote));
            } else {
                //全削除
                _menuStrip.Items.Clear();
                _ar.Clear();

                var subMenu = new ListMenu{new OneMenu("File_Exit", "終了(&X)", "Exit")};

                //「ファイル」メニュー
                var m = AddOneMenu(_menuStrip.Items, new OneMenu("File", "ファイル(&F)", "&File"));
                AddListMenu(m, subMenu);

            }
        }
        //メニュー構築（内部テーブルの初期化）
        public void Initialize() {
            if (_menuStrip == null)
                return;

            if (_menuStrip.InvokeRequired) {
                _menuStrip.Invoke(new MethodInvoker(Initialize));
            } else {
                //全削除
                _menuStrip.Items.Clear();
                _ar.Clear();

                //「ファイル」メニュー
                var m = AddOneMenu(_menuStrip.Items, new OneMenu("File", "ファイル(&F)", "&File"));
                AddListMenu(m, FileMenu());

                //「オプション」メニュー
                m = AddOneMenu(_menuStrip.Items, new OneMenu("Option", "オプション(&O)", "&Option"));
                AddListMenu(m, _kernel.ListOption.Menu());

                //「ツール」メニュー
                m = AddOneMenu(_menuStrip.Items, new OneMenu("Tool", "ツール(&T)", "&Tool"));
                AddListMenu(m, _kernel.ListTool.Menu());

                //「起動/停止」メニュー
                m = AddOneMenu(_menuStrip.Items, new OneMenu("StartStop", "起動/停止(&S)", "&Start/Stop"));
                AddListMenu(m, StartStopMenu());

                //「ヘルプ」メニュー
                m = AddOneMenu(_menuStrip.Items, new OneMenu("Help", "ヘルプ(&H)", "&Help"));
                AddListMenu(m, HelpMenu());


                SetJang();
                SetEnable();//状況に応じた有効無効
            }
        }

        //メニュー選択のイベントを発生させる
        //synchro=false 非同期で動作する
        public void EnqueueMenu(string name, bool synchro) {
            var item = new ToolStripMenuItem{Name = name};
            if (synchro) {
                if (OnClick != null) {
                    OnClick(item);
                }
            } else {
                _queue.Enqueue(item);//キューに格納する
            }
        }
        //タイマー起動でキューに入っているメニューイベントを実行する
        void TimerElapsed(object sender, System.Timers.ElapsedEventArgs e) {
            if(_queue.Count>0){
                if (OnClick != null) {
                    var q = _queue.Dequeue();
                    OnClick(q);
                }
            }
        }

        //ListMenuの追加
        void AddListMenu(ToolStripMenuItem owner, ListMenu subMenu) {
            foreach (var o in subMenu) {
                AddOneMenu(owner.DropDownItems, o);
            }
        }
        //OneMenuの追加
        ToolStripMenuItem AddOneMenu(ToolStripItemCollection items, OneMenu o) {
            if (o.Name == "-") {
                items.Add("-");//ToolStripSeparatorが生成される
                return null;
            }
            var item = (ToolStripMenuItem)items.Add(o.JpTitle);
            item.Name = o.Name;//名前
            item.ShortcutKeys = o.Keys;//ショッートカット
            item.Click += MenuItemClick;//クリックイベンント
            AddListMenu(item, o.SubMenu);//再帰処理(o.SubMenu.Count==0の時、処理なしで戻ってくる)

            _ar.Add(o,item);//内部テーブルへの追加
            return item;
        }
        //メニュー選択時のイベント処理
        void MenuItemClick(object sender, EventArgs e) {
            if (OnClick != null) {
                OnClick((ToolStripMenuItem)sender);
            }
        }
        //言語設定
        void SetJang() {
            foreach (var o in _ar) {
                o.Value.Text = _kernel.IsJp()?o.Key.JpTitle:o.Key.EnTitle;
            }
        }
        //状況に応じた有効/無効
        public void SetEnable() {
            if (_kernel.RunMode == RunMode.NormalRegist) {//サービス登録されている場合
                //サーバの起動停止はできない
                SetEnabled("StartStop_Start", false);
                SetEnabled("StartStop_Stop", false);
                SetEnabled("StartStop_Restart", false);
                SetEnabled("StartStop_Service", true);
                SetEnabled("File_LogClear", false);
                SetEnabled("File_LogCopy", false);
                SetEnabled("File_Trace", false);
                SetEnabled("Tool", false);
            }else if (_kernel.RunMode == RunMode.Remote) {//リモートクライアント
                //サーバの再起動のみ
                SetEnabled("StartStop_Start", false);
                SetEnabled("StartStop_Stop", false);
                SetEnabled("StartStop_Restart", true);
                SetEnabled("StartStop_Service", false);
                SetEnabled("File_LogClear", true);
                SetEnabled("File_LogCopy", true);
                SetEnabled("File_Trace", true);
                SetEnabled("Tool", true);
            } else {//通常起動
                SetEnabled("StartStop_Start", !_kernel.ListServer.IsRunnig);
                SetEnabled("StartStop_Stop", _kernel.ListServer.IsRunnig);
                SetEnabled("StartStop_Restart", _kernel.ListServer.IsRunnig);
                SetEnabled("StartStop_Service", !_kernel.ListServer.IsRunnig);
                SetEnabled("File_LogClear", true);
                SetEnabled("File_LogCopy", true);
                SetEnabled("File_Trace", true);
                SetEnabled("Tool", true);
            }
        }

        //「ファイル」メニュー
        ListMenu FileMenu() {
            var subMenu = new ListMenu();
            subMenu.Add(new OneMenu("File_LogClear","ログクリア(&C)","Log&Clear", Keys.F1));
            subMenu.Add(new OneMenu("File_LogCopy","ログコピー(&L)","&LogCopy", Keys.F2));
            subMenu.Add(new OneMenu("File_Trace","トレース表示(&T)","&Trace"));
            subMenu.Add(new OneMenu("-", "", ""));
            subMenu.Add(new OneMenu("File_Exit", "終了(&X)", "Exit"));
            return subMenu;        
        }
        //「起動/停止」メニュー
        ListMenu StartStopMenu() {
            var subMenu = new ListMenu();
            subMenu.Add(new OneMenu("StartStop_Start","サーバ起動(&T)","&Start"));
            subMenu.Add(new OneMenu("StartStop_Stop","サーバ停止(&P)","&Stop"));
            subMenu.Add(new OneMenu("StartStop_Restart","サーバ再起動(&R)","&Restart", keys: Keys.Control | Keys.X));
            subMenu.Add(new OneMenu("StartStop_Service","サービス設定(&S)","&Service"));
            return subMenu;
        }
        //「ヘルプ」メニュー
        ListMenu HelpMenu() {
            var subMenu = new ListMenu();
            subMenu.Add(new OneMenu("Help_Homepage","ホームページ(&H)","&HomePage"));
            subMenu.Add(new OneMenu("Help_Document","ドキュメント(&D)","&Document"));
            subMenu.Add(new OneMenu("Help_Support", "サポート掲示板(&S)", "&Support"));
            subMenu.Add(new OneMenu("Help_Version", "バージョン情報(&V)", "&Version"));
            return subMenu;
        }
    }

}
    */