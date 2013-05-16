﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bjd;
using Bjd.log;
using Bjd.net;
using Bjd.option;
using Bjd.server;
using Bjd.sock;
using Bjd.util;

namespace DnsServer{


    public class Server : OneServer{

        //キャッシュ
        private RrDb _rootCache;
        private List<RrDb> _cacheList;

        private readonly Kernel _kernel;

        public Server(Kernel kernel, Conf conf, OneBind oneBind) : base(kernel,conf, oneBind){
            _kernel = kernel;

        }


        protected override void OnStopServer(){

        }


        protected override bool OnStartServer(){
            //ルートキャッシュの初期化
            _rootCache = null;
            var namedCaPath = string.Format("{0}\\{1}", _kernel.ProgDir(), Conf.Get("rootCache"));
            if (File.Exists(namedCaPath)){
                try{
                    //named.ca読み込み用コンストラクタ
                    var expire = (int)Conf.Get("soaExpire");
                    //var expire = (uint)Conf.Get("soaExpire");
                    _rootCache = new RrDb(namedCaPath, (uint)expire);
                    Logger.Set(LogKind.Detail, null, 6, namedCaPath);

                } catch (IOException){
                    Logger.Set(LogKind.Error, null, 2, string.Format("filename={0}", namedCaPath));
                }
            } else{
                Logger.Set(LogKind.Error, null, 3, namedCaPath);
            }

            //設定したドメイン情報を初期化する
            if (_cacheList != null){
                _cacheList.Clear();
            }
            _cacheList = new List<RrDb>();
            var op = _kernel.ListOption.Get("DnsDomain");
            if (op != null){
                var domainList = (Dat) op.GetValue("domainList");
                if (domainList != null){
                    foreach (OneDat o in domainList){
                        if (o.Enable){
                            //ドメインごとのリソースの読込
                            var domainName = o.StrList[0];
                            var res = _kernel.ListOption.Get("Resource-" + domainName);
                            if (res != null){
                                var resource = (Dat) res.GetValue("resourceList");
                                var rrDb = new RrDb(Logger, Conf, resource, domainName);
                                _cacheList.Add(rrDb);
                                Logger.Set(LogKind.Detail, null, 21, "Resource-" + domainName);
                            }
                        }
                    }
                }
            }

            return true;
        }

        //リクエストのドメイン名を取得する
        private string InitRequestDomain(string requestName, DnsType dnsType){

            var name = "";

            //.が存在する場合、.以降をデフォルト値として仮置きする
            var index = requestName.IndexOf('.');
            if (index != -1 && index < requestName.Length - 1){
                name = requestName.Substring(index + 1);
            }

            if (dnsType == DnsType.A || dnsType == DnsType.Aaaa || dnsType == DnsType.Cname){
                // （ドメイン名自身にアドレスが指定されている可能性が有る）
                // A CNAME の場合、リクエスト名がホスト名を含まないドメイン名である可能性があるため
                // 対象ドメインのキャッシュからＡレコードが存在するかどうかの確認を行う
                foreach (var cache in _cacheList){
                    if (cache.GetDomainName() == requestName){
                        if (cache.Find(requestName, DnsType.A)){
                            name = requestName;
                        }
                    }

                }
            } else if (dnsType == DnsType.Mx || dnsType == DnsType.Ns || dnsType == DnsType.Soa){
                //MX NS SOA リクエストの場合亜h、requestName自体がドメイン名となる
                name = requestName;
            }

            if (requestName.ToUpper() == "LOCALHOST."){
                name = "localhost.";
            }
            return name;

        }


        protected override void OnSubThread(SockObj sockObj){
            var sockUdp = (SockUdp) sockObj;
            //セッションごとの情報
            //Session session = new Session((SockTcp) sockObj);

            PacketDns rp; //受信パケット
            try{
                //パケットの読込(受信パケットrp)  
                var buf = sockUdp.RecvBuf;
                if ( buf.Length < 12){
                    return;
                }
                rp = new PacketDns(sockUdp.RecvBuf);
            } catch (IOException) {
                //データ解釈に失敗した場合は、処理なし
                Logger.Set(LogKind.Secure, sockUdp, 4, ""); //不正パケットの可能性あり 
                return;
            }
            //リクエストのドメイン名を取得する
            var domainName = InitRequestDomain(rp.GetRequestName(), rp.GetDnsType());

            //リクエスト解釈完了
            Logger.Set(LogKind.Normal, sockUdp, 8, string.Format("{0} {1} domain={2}", rp.GetDnsType(), rp.GetRequestName(), domainName)); //Query

            var aa = false; // ドメインオーソリティ(管理ドメインかそうでないか)
            const bool ra = true; //再帰可能

            var targetCache = _rootCache; //デフォルトはルートキャッシュ

            if (rp.GetDnsType() == DnsType.Ptr){
                if (rp.GetRequestName().ToUpper() == "1.0.0.127.IN-ADDR.ARPA." || rp.GetRequestName().ToUpper() == "1.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.IP6.ARPA." || rp.GetRequestName().ToUpper() == "LOCALHOST."){
                    //キャッシュはデフォルトであるルートキャッシュが使用される
                    aa = true;
                    Logger.Set(LogKind.Detail, sockUdp, 9, ""); //"request to a domain under auto (localhost)"
                } else{
                    foreach (var cache in _cacheList){
                        if (cache.Find(rp.GetRequestName(), DnsType.Ptr)){
                            targetCache = cache;
                            aa = true;
                            Logger.Set(LogKind.Detail, sockUdp, 10, string.Format("Resource={0}", targetCache.GetDomainName())); //"request to a domain under management"
                            break;
                        }
                    }
                }
            } else{
                //A
                if (rp.GetRequestName().ToUpper() == "LOCALHOST."){
                    //キャッシュはデフォルトであるルートキャッシュが使用される
                    aa = true;
                    Logger.Set(LogKind.Detail, sockUdp, 11, ""); //"request to a domain under auto (localhost)"
                } else{
                    foreach (var cache in _cacheList){
                        if (cache.GetDomainName().ToUpper() == domainName.ToUpper()){
                            //大文字で比較される
                            targetCache = cache;
                            aa = true;
                            Logger.Set(LogKind.Detail, sockUdp, 12, string.Format("Resource={0}", domainName)); //"request to a domain under management"
                            break;
                        }
                    }

                }
            }

            //管理するドメインでなく、かつ 再帰要求が無い場合は、処理を終わる
            if (!(aa) && !(rp.GetRd())){
                return;
            }

            //aa ドメインオーソリティ
            //rs 再帰可能
            //rd 再起要求あり

            // (A)「ヘッダ」作成
            const bool qr = true; //応答

            //********************************************************
            //パケットの生成(送信パケットsp)            
            //********************************************************
            var sp = new PacketDns(rp.GetId(), qr, aa, rp.GetRd(), ra);

            // (B)「質問セクション」の追加
            AppendRr(sp, RrKind.QD, new RrQuery(rp.GetRequestName(), rp.GetDnsType()));//質問フィールドの追加
            if (!aa){
                //ドメインオーソリティ（権威サーバ）で無い場合
                //ルートキャッシュにターゲットのデータが蓄積されるまで、再帰的に検索する
                const int depth = 0;
                try{
                    SearchLoop(rp.GetRequestName(), rp.GetDnsType(), depth, sockUdp.RemoteIp);
                } catch (IOException){
                    // ここはどうやって扱えばいいか？？？
                    //e.printStackTrace();
                }
            }

            // (B)「回答セクション」作成
            var ansList = targetCache.GetList(rp.GetRequestName(), rp.GetDnsType());

            Logger.Set(LogKind.Detail, sockUdp, 13, string.Format("{0} ansList.Count={1}", rp.GetDnsType(), ansList.Count)); //"Create Response (AN)"
            if (0 < ansList.Count){
                //検索でヒットした場合
                foreach (var oneRr in ansList){
                    AppendRr(sp, RrKind.AN, DnsUtil.CreateRr(rp.GetRequestName(), rp.GetDnsType(), oneRr.Ttl, oneRr.Data));

                    if (rp.GetDnsType() == DnsType.Mx || rp.GetDnsType() == DnsType.Cname || rp.GetDnsType() == DnsType.Ns){

                        var targetName = "";
                        if (rp.GetDnsType() == DnsType.Mx){
                            targetName = ((RrMx) oneRr).MailExchangeHost;
                        } else if (rp.GetDnsType() == DnsType.Ns){
                            targetName = ((RrNs) oneRr).NsName;
                        } else if (rp.GetDnsType() == DnsType.Cname){
                            targetName = ((RrCname) oneRr).CName;
                        } else{
                            Util.RuntimeException("not implement [Server.onSubThread()]");
                        }

                        //追加情報が必要な場合 （Aレコード）をパケットに追加する
                        var rr = targetCache.GetList(targetName, DnsType.A);
                        foreach (OneRr r in rr){
                            AppendRr(sp, RrKind.AR, new RrA(targetName, r.Ttl, r.Data));
                        }

                        //追加情報が必要な場合 （AAAAレコード）をパケットに追加する
                        rr = targetCache.GetList(targetName, DnsType.Aaaa);
                        foreach (var r in rr){
                            AppendRr(sp, RrKind.AR, new RrAaaa(targetName, r.Ttl, r.Data));
                        }
                    }
                }
            } else{
                //検索でヒットしない場合
                if (rp.GetDnsType() == DnsType.A){
                    // CNAMEに定義されていないかどうかを確認する
                    var cnameList = targetCache.GetList(rp.GetRequestName(), DnsType.Cname);
                    foreach (var o in cnameList){
                        var cname = ((RrCname) o).CName;
                        var aList = targetCache.GetList(cname, DnsType.A);
                        foreach (var a in aList){
                            Logger.Set(LogKind.Detail, sockUdp, 16, o.ToString()); //"Append RR"
                            AppendRr(sp, RrKind.AN, o);
                            Logger.Set(LogKind.Detail, sockUdp, 16, a.ToString()); //"Append RR"
                            AppendRr(sp, RrKind.AN, a);
                        }
                    }
                }
            }

            if (rp.GetDnsType() == DnsType.A || rp.GetDnsType() == DnsType.Aaaa || rp.GetDnsType() == DnsType.Soa || rp.GetDnsType() == DnsType.Cname){
                // (C)「権威セクション」「追加情報セクション」作成
                var nsList = targetCache.GetList(domainName, DnsType.Ns);
                Logger.Set(LogKind.Detail, sockUdp, 22, string.Format("{0} nsList.Count={1}", DnsType.Ns, nsList.Count)); // Create Response (AR)
                foreach (var o in nsList){
                    var ns = (RrNs) o;
                    
                    AppendRr(sp, RrKind.NS, new RrNs(ns.Name, ns.Ttl, ns.Data));

                    if (domainName.ToUpper() != "LOCALHOST."){
                        //localhost検索の場合は、追加情報はない
                        //「追加情報」
                        var addList = targetCache.GetList(ns.NsName, DnsType.A);
                        foreach (OneRr rr in addList){
                            AppendRr(sp, RrKind.AR, new RrA(ns.NsName, rr.Ttl, rr.Data));
                        }
                        addList = targetCache.GetList(ns.NsName, DnsType.Aaaa);
                        foreach (OneRr rr in addList){
                            AppendRr(sp, RrKind.AR, new RrAaaa(ns.NsName, rr.Ttl, rr.Data));
                        }
                    }
                }
            }

            sockUdp.Send(sp.GetBytes()); //送信
            //sockUdp.Close();UDPソケット(sockUdp)はクローンなのでクローズしても、処理されない※Close()を呼び出しても問題はない
            sockUdp.Close();
        }

        //レスポンス情報追加をまとめて記述
        void AppendRr(PacketDns packetDns,RrKind rrKind,OneRr oneRr){
            Logger.Set(LogKind.Detail, null, 23, string.Format("[{0}] {1}", rrKind, oneRr)); //"Append RR"
            packetDns.AddRr(rrKind, oneRr);
        }

        //addrは通常オーダで指定されている
        //private PacketDns Lookup(Ip ip, string requestName, DNS_TYPE dnsType,RemoteInfo remoteInfo) {
        private PacketDns Lookup(Ip ip, string requestName, DnsType dnsType, Ip remoteAddr){

            //Ip ip = new Ip(addr);
            Logger.Set(LogKind.Detail, null, 17, string.Format("{0} Server={1} Type={2}", requestName, ip, dnsType)); //"Lookup"

            //受信タイムアウト
            const int timeout = 3;

            //		var random = new Random(Environment.TickCount);
            //		var id = (ushort) random.Next(0xFFFF);//識別子をランダムに決定する
            var random = new Random();
            var id = (ushort)random.Next(0xFFFF);
            const bool qr = false; //要求
            const bool aa = false; //権威なし
            var rd = (bool) Conf.Get("useRD"); //再帰要求を使用するかどうか
            const bool ra = false; //再帰無効

            //リクエストパケットの生成
            var sp = new PacketDns(id, qr, aa, rd, ra);

            AppendRr(sp, RrKind.QD, new RrQuery(requestName, dnsType));//QR(質問)フィールド追加

            const int port = 53;
            //SockUdp sockUdp = new UdpObj(Kernel, getLogger(), ip, port);
            byte[] sendBuf = sp.GetBytes();
            var sockUdp = new SockUdp(_kernel,ip, port, null, sendBuf); //送信

            //この辺のロジックを動作確認する必要がある
            byte[] recvBuf = sockUdp.Recv(timeout);
            
            if (recvBuf != null && 12 <= recvBuf.Length){
                //受信

                try{
                    var rp = new PacketDns(recvBuf);

                    var str = string.Format("{0} count[{1},{2},{3},{4}] rcode={5} AA={6}", requestName, rp.GetCount(RrKind.QD), rp.GetCount(RrKind.AN), rp.GetCount(RrKind.NS), rp.GetCount(RrKind.AR), rp.GetRcode(), rp.GetAa());
                    Logger.Set(LogKind.Detail, sockUdp, 18, str); //"Lookup"

                    //質問フィールの以外のリソースデータをキャッシュする
                    //for (int rr = 1; rr < 4; rr++) {
                    foreach (RrKind rr in Enum.GetValues(typeof (RrKind))){
                        if (rr == RrKind.QD){
                            continue; //質問フィールの以外のリソースデータをキャッシュする
                        }
                        var m = rp.GetCount(rr);
                        for (var n = 0; n < m; n++){
                            var oneRr = rp.GetRr(rr, n);
                            _rootCache.Add(oneRr);
                            Logger.Set(LogKind.Error, sockUdp, 24, string.Format("{0} _rootCache.Count={1}",oneRr,_rootCache.Count)); //_rootCache.Add
                        }
                    }
                    return rp;
                } catch (IOException){
                    //ここでのエラーログも必要？
                    return null;
                }
            }

            Logger.Set(LogKind.Error, sockUdp, 5, string.Format("addr={0} requestName={1} dnsType={2}", remoteAddr, requestName, dnsType)); //Lookup() パケット受信でタイムアウトが発生しました。
            return null;
        }

        //ルートキャッシュにターゲットのデータが蓄積されるまで、再帰的に検索する
        private bool SearchLoop(string requestName, DnsType dnsType, int depth, Ip remoteAddr){

            if (depth > 15){
                return false;
            }

            //リクエスト名からドメイン名を取得する
            var domainName = GetDomainName(requestName);

            //対象ドメインのNSサーバ一覧を取得する(存在しない場合は、ルートNSの一覧となる)
            var nsList = GetNsList(domainName);
            
            while (true){
                //検索が完了しているかどうかの確認
                //rootCacheにターゲットのデータがキャッシュ（蓄積）されているか
                if (_rootCache.Find(requestName, dnsType)){
                    return true; //検索完了
                }
                if (dnsType == DnsType.A){
                    //DNS_TYPE.Aの場合、CNAMEがキャッシュされている場合、蓄積完了となる
                    var rrList = _rootCache.GetList(requestName, DnsType.Cname);
                    foreach (var o in rrList){
                        if (_rootCache.Find(((RrCname)o).CName, DnsType.A)){
                            return true;
                        }
                    }
                }

                //ネームサーバ一覧から、そのアドレスの一覧を作成する
                var nsIpList = GetIpList(nsList,depth,remoteAddr);
                //ネームサーバのアドレスが取得できない場合、処理の継続はできない（検索不能）
                if (nsIpList.Count == 0) {
                    return false;
                }
                nsList.Clear();


                //ターゲットの取得成功  return true
                //権威サーバから回答なし return false
                //NSリストが取得できな場合 nsListを作成してして break
                foreach (var ip in nsIpList){

                    var rp = Lookup(ip, requestName, dnsType, remoteAddr);
                    if (rp != null){
                        if (rp.GetAa()){
                            //権威サーバの応答の場合
                            //ホストが存在しない　若しくは　回答フィールドが0の場合、処理停止
                            if (rp.GetRcode() == 3 || rp.GetCount(RrKind.AN) == 0){
                                return false;
                            }
                        }
                        if (0 < rp.GetCount(RrKind.AN)){
                            //回答フィールドが存在する場合
                            return true;
                        }
                        // 求めている回答は得ていないが、権威サーバを教えられた場合
                        // ネームサーバのリストを差し替える
                        for (var n = 0; n < rp.GetCount(RrKind.NS); n++){
                            var oneRr = rp.GetRr(RrKind.NS, n);
                            if (oneRr.DnsType == DnsType.Ns){
                                nsList.Add(((RrNs)oneRr).NsName);
                            }
                        }
                        if (0 < nsList.Count){
                            break; //ネームサーバリストを取得した
                        }
                    }
                }
            }
        }

        //ネームサーバ一覧から、そのアドレスの一覧を作成する
        List<Ip> GetIpList(IEnumerable<string> nsList,int depth,Ip remoteAddr) {
            var ipList = new List<Ip>();
            foreach (var ns in nsList) {
                var rrList = _rootCache.GetList(ns, DnsType.A);

                //IP情報が無い場合、再帰検索
                if (rrList.Count == 0){
                    SearchLoop(ns, DnsType.A, depth, remoteAddr);
                    rrList = _rootCache.GetList(ns, DnsType.A);
                }

                rrList.AddRange(_rootCache.GetList(ns, DnsType.Aaaa));

                foreach (var o in rrList) {
                    Ip ip = null;
                    if (o.DnsType == DnsType.A) {
                        ip = ((RrA)o).Ip;
                    } else if (o.DnsType == DnsType.Aaaa) {
                        ip = ((RrAaaa)o).Ip;
                    }
                    //重複は追加しない
                    if (ipList.IndexOf(ip) == -1){
                        ipList.Add(ip);
                    }
                }

            }
            return ipList;
        }

        //リクエスト名からドメイン名を取得する
        string GetDomainName(string requestName){
            var domainName = requestName;
            var index = requestName.IndexOf('.');
            if (index != -1) {
                domainName = requestName.Substring(index + 1);
            }
            return domainName;
        }


        //対象ドメインのNSサーバ一覧を取得する(存在しない場合は、ルートNSの一覧となる)
        List<string> GetNsList(string domainName){
            var nsList = new List<string>();
            var rrList = _rootCache.GetList(domainName, DnsType.Ns);
            if (0 < rrList.Count) {
                nsList.AddRange(rrList.Select(o => ((RrNs)o).NsName));
            } else { //キャッシュに存在しない場合
                //ルートNSサーバをランダムに一覧セットする
                nsList = GetRootNsList();
            }
            return nsList;
        }

        //ルートNSサーバをランダムに一覧セットする
        List<string> GetRootNsList(){
            var nsList = new List<string>();
            var rrList = _rootCache.GetList(".", DnsType.Ns);

            var center = 0;
            if (rrList.Count > 0) {
                var random = new Random();
                center = random.Next(rrList.Count); //センタ位置をランダムに決定する
            }

            for (int i = center; i < rrList.Count; i++) {
                //センタ以降の一覧を取得
                nsList.Add(((RrNs)rrList[i]).NsName);
            }
            for (int i = 0; i < center; i++) {
                //センタ以前の一覧をコピー
                nsList.Add(((RrNs)rrList[i]).NsName);
            }
            return nsList;
        }



        public override string GetMsg(int messageNo){
            switch (messageNo){
                    //case 0:
                    //	return IsJp ? "標準問合(OPCODE=0)以外のリクエストには対応できません" : "Because I am different from 0 in OPCODE,can't process it.";
                    //case 1:
                    //	return IsJp ? "質問エントリーが１でないパケットは処理できません" : "Because I am different from 1 a question entry,can't process it.";
                case 2:
                    return IsJp ? "ルートキャッシュの読み込みに失敗しました" : "Failed in reading of route cash.";
                case 3:
                    return IsJp ? "ルートキャッシュ(ファイル)が見つかりません" : "Root chace (file) is not found";
                case 4:
                    return IsJp ? "パケットの解釈に失敗しました。正常なDNSリクエストでない可能性があります。" : "Failed in interpretation of a packet.It may not be a normal DNS request.";
                case 5:
                    return IsJp ? "Lookup() パケット受信でタイムアウトが発生しました。" : "Timeout occurred in Lookup()";
                case 6:
                    return IsJp ? "ルートキャッシュを読み込みました" : "root cache database initialised.";
                case 7:
                    return "zone database initialised.";
                case 8:
                    return "Query recv"; 
                case 9:
                    return "request to a domain under auto (localhost)";
                case 10:
                    return "request to a domain under management";
                case 11:
                    return "request to a domain under auto (localhost)";
                case 12:
                    return "request to a domain under management";
                case 13:
                    return "Create Response (AN)";
                //case 14:
                case 15:
                    return "Create Response (AN.CNAME)";
                //case 16:
                case 17:
                    return "Lookup send";
                case 18:
                    return "Lookup recv";
                case 19:
                    return IsJp ? "リソースデータの読み込みに失敗しました" : "Failed in reading of resource data";
                case 20:
                    return IsJp ? "リソース(SOA)は追加されませんでした" : "Resource (SOA) was not added";
                case 21:
                    return IsJp ? "ドメインのリソース定義を読み込みました" : "Read a resource definition of a domain";
                case 22:
                    return "Create Response (AR)";
                case 23:
                    return "Append RR";
                case 24:
                    return "_rootCache.Add";

                default:
                    return "unknown";
            }
        }
        //RemoteServerでのみ使用される
        public override void Append(OneLog oneLog) {

        }

    }
}