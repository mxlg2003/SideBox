namespace WinDock.Core;

public sealed record WeatherCityItem(string Name, string Admin, string Country = "中国", string Pinyin = "")
{
    public string DisplayLocation => string.IsNullOrWhiteSpace(Admin) ? Country : $"{Admin} · {Country}";
}

public static class WeatherCities
{
    public static readonly IReadOnlyList<WeatherCityItem> Database =
    [
        // Municipalities
        new("北京", "北京市", Pinyin: "beijing"),
        new("上海", "上海市", Pinyin: "shanghai"),
        new("天津", "天津市", Pinyin: "tianjin"),
        new("重庆", "重庆市", Pinyin: "chongqing"),

        // Jiangsu
        new("南京", "江苏省", Pinyin: "nanjing"),
        new("苏州", "江苏省", Pinyin: "suzhou"),
        new("无锡", "江苏省", Pinyin: "wuxi"),
        new("常州", "江苏省", Pinyin: "changzhou"),
        new("南通", "江苏省", Pinyin: "nantong"),
        new("扬州", "江苏省", Pinyin: "yangzhou"),
        new("徐州", "江苏省", Pinyin: "xuzhou"),
        new("盐城", "江苏省", Pinyin: "yancheng"),
        new("镇江", "江苏省", Pinyin: "zhenjiang"),
        new("泰州", "江苏省", Pinyin: "taizhou"),
        new("宿迁", "江苏省", Pinyin: "suqian"),
        new("连云港", "江苏省", Pinyin: "lianyungang"),
        new("淮安", "江苏省", Pinyin: "huaian"),

        // Jiangxi
        new("南昌", "江西省", Pinyin: "nanchang"),
        new("九江", "江西省", Pinyin: "jiujiang"),
        new("赣州", "江西省", Pinyin: "ganzhou"),
        new("吉安", "江西省", Pinyin: "jian"),
        new("宜春", "江西省", Pinyin: "yichun"),
        new("抚州", "江西省", Pinyin: "fuzhou"),
        new("上饶", "江西省", Pinyin: "shangrao"),
        new("景德镇", "江西省", Pinyin: "jingdezhen"),
        new("萍乡", "江西省", Pinyin: "pingxiang"),
        new("新余", "江西省", Pinyin: "xinyu"),
        new("鹰潭", "江西省", Pinyin: "yingtan"),

        // Guangxi
        new("南宁", "广西壮族自治区", Pinyin: "nanning"),
        new("桂林", "广西壮族自治区", Pinyin: "guilin"),
        new("柳州", "广西壮族自治区", Pinyin: "liuzhou"),
        new("北海", "广西壮族自治区", Pinyin: "beihai"),
        new("梧州", "广西壮族自治区", Pinyin: "wuzhou"),
        new("玉林", "广西壮族自治区", Pinyin: "yulin"),
        new("百色", "广西壮族自治区", Pinyin: "baise"),

        // Henan
        new("郑州", "河南省", Pinyin: "zhengzhou"),
        new("洛阳", "河南省", Pinyin: "luoyang"),
        new("开封", "河南省", Pinyin: "kaifeng"),
        new("南阳", "河南省", Pinyin: "nanyang"),
        new("安阳", "河南省", Pinyin: "anyang"),
        new("新乡", "河南省", Pinyin: "xinxiang"),
        new("焦作", "河南省", Pinyin: "jiaozuo"),
        new("许昌", "河南省", Pinyin: "xuchang"),
        new("平顶山", "河南省", Pinyin: "pingdingshan"),
        new("信阳", "河南省", Pinyin: "xinyang"),
        new("商丘", "河南省", Pinyin: "shangqiu"),
        new("周口", "河南省", Pinyin: "zhoukou"),
        new("驻马店", "河南省", Pinyin: "zhumadian"),

        // Sichuan
        new("成都", "四川省", Pinyin: "chengdu"),
        new("绵阳", "四川省", Pinyin: "mianyang"),
        new("德阳", "四川省", Pinyin: "deyang"),
        new("宜宾", "四川省", Pinyin: "yibin"),
        new("南充", "四川省", Pinyin: "nanchong"),
        new("泸州", "四川省", Pinyin: "luzhou"),
        new("达州", "四川省", Pinyin: "dazhou"),
        new("乐山", "四川省", Pinyin: "leshan"),

        // Fujian
        new("福州", "福建省", Pinyin: "fuzhou"),
        new("厦门", "福建省", Pinyin: "xiamen"),
        new("泉州", "福建省", Pinyin: "quanzhou"),
        new("漳州", "福建省", Pinyin: "zhangzhou"),
        new("南平", "福建省", Pinyin: "nanping"),
        new("莆田", "福建省", Pinyin: "putian"),
        new("龙岩", "福建省", Pinyin: "longyan"),
        new("三明", "福建省", Pinyin: "sanming"),
        new("宁德", "福建省", Pinyin: "ningde"),

        // Shandong
        new("济南", "山东省", Pinyin: "jinan"),
        new("青岛", "山东省", Pinyin: "qingdao"),
        new("烟台", "山东省", Pinyin: "yantai"),
        new("潍坊", "山东省", Pinyin: "weifang"),
        new("威海", "山东省", Pinyin: "weihai"),
        new("淄博", "山东省", Pinyin: "zibo"),
        new("临沂", "山东省", Pinyin: "linyi"),
        new("济宁", "山东省", Pinyin: "jining"),
        new("泰安", "山东省", Pinyin: "taian"),

        // Anhui
        new("合肥", "安徽省", Pinyin: "hefei"),
        new("芜湖", "安徽省", Pinyin: "wuhu"),
        new("蚌埠", "安徽省", Pinyin: "bengbu"),
        new("淮南", "安徽省", Pinyin: "huainan"),
        new("马鞍山", "安徽省", Pinyin: "maanshan"),
        new("淮北", "安徽省", Pinyin: "huaibei"),
        new("铜陵", "安徽省", Pinyin: "tongling"),
        new("安庆", "安徽省", Pinyin: "anqing"),
        new("黄山", "安徽省", Pinyin: "huangshan"),
        new("滁州", "安徽省", Pinyin: "chuzhou"),
        new("阜阳", "安徽省", Pinyin: "fuyang"),
        new("宿州", "安徽省", Pinyin: "suzhou"),
        new("六安", "安徽省", Pinyin: "luan"),
        new("亳州", "安徽省", Pinyin: "bozhou"),
        new("池州", "安徽省", Pinyin: "chizhou"),
        new("宣城", "安徽省", Pinyin: "xuancheng"),

        // Zhejiang
        new("杭州", "浙江省", Pinyin: "hangzhou"),
        new("宁波", "浙江省", Pinyin: "ningbo"),
        new("温州", "浙江省", Pinyin: "wenzhou"),
        new("嘉兴", "浙江省", Pinyin: "jiaxing"),
        new("湖州", "浙江省", Pinyin: "huzhou"),
        new("绍兴", "浙江省", Pinyin: "shaoxing"),
        new("金华", "浙江省", Pinyin: "jinhua"),
        new("衢州", "浙江省", Pinyin: "quzhou"),
        new("舟山", "浙江省", Pinyin: "zhoushan"),
        new("台州", "浙江省", Pinyin: "taizhou"),
        new("丽水", "浙江省", Pinyin: "lishui"),

        // Guangdong
        new("广州", "广东省", Pinyin: "guangzhou"),
        new("深圳", "广东省", Pinyin: "shenzhen"),
        new("珠海", "广东省", Pinyin: "zhuhai"),
        new("佛山", "广东省", Pinyin: "foshan"),
        new("东莞", "广东省", Pinyin: "dongguan"),
        new("中山", "广东省", Pinyin: "zhongshan"),
        new("惠州", "广东省", Pinyin: "huizhou"),
        new("江门", "广东省", Pinyin: "jiangmen"),
        new("汕头", "广东省", Pinyin: "shantou"),
        new("湛江", "广东省", Pinyin: "zhanjiang"),
        new("肇庆", "广东省", Pinyin: "zhaoqing"),
        new("清远", "广东省", Pinyin: "qingyuan"),

        // Hubei
        new("武汉", "湖北省", Pinyin: "wuhan"),
        new("宜昌", "湖北省", Pinyin: "yichang"),
        new("襄阳", "湖北省", Pinyin: "xiangyang"),
        new("荆州", "湖北省", Pinyin: "jingzhou"),
        new("黄石", "湖北省", Pinyin: "huangshi"),
        new("十堰", "湖北省", Pinyin: "shiyan"),

        // Hunan
        new("长沙", "湖南省", Pinyin: "changsha"),
        new("株洲", "湖南省", Pinyin: "zhuzhou"),
        new("湘潭", "湖南省", Pinyin: "xiangtan"),
        new("衡阳", "湖南省", Pinyin: "hengyang"),
        new("岳阳", "湖南省", Pinyin: "yueyang"),
        new("常德", "湖南省", Pinyin: "changde"),
        new("张家界", "湖南省", Pinyin: "zhangjiajie"),

        // Shaanxi
        new("西安", "陕西省", Pinyin: "xian"),
        new("宝鸡", "陕西省", Pinyin: "baoji"),
        new("咸阳", "陕西省", Pinyin: "xianyang"),
        new("渭南", "陕西省", Pinyin: "weinan"),
        new("延安", "陕西省", Pinyin: "yanan"),
        new("汉中", "陕西省", Pinyin: "hanzhong"),
        new("榆林", "陕西省", Pinyin: "yulin"),

        // Shanxi
        new("太原", "山西省", Pinyin: "taiyuan"),
        new("大同", "山西省", Pinyin: "datong"),
        new("长治", "山西省", Pinyin: "changzhi"),
        new("运城", "山西省", Pinyin: "yuncheng"),

        // Hebei
        new("石家庄", "河北省", Pinyin: "shijiazhuang"),
        new("唐山", "河北省", Pinyin: "tangshan"),
        new("保定", "河北省", Pinyin: "baoding"),
        new("邯郸", "河北省", Pinyin: "handan"),
        new("秦皇岛", "河北省", Pinyin: "qinhuangdao"),
        new("张家口", "河北省", Pinyin: "zhangjiakou"),

        // Liaoning
        new("沈阳", "辽宁省", Pinyin: "shenyang"),
        new("大连", "辽宁省", Pinyin: "dalian"),
        new("鞍山", "辽宁省", Pinyin: "anshan"),
        new("锦州", "辽宁省", Pinyin: "jinzhou"),

        // Jilin & Heilongjiang
        new("长春", "吉林省", Pinyin: "changchun"),
        new("吉林", "吉林省", Pinyin: "jilin"),
        new("哈尔滨", "黑龙江省", Pinyin: "haerbin"),
        new("大庆", "黑龙江省", Pinyin: "daqing"),
        new("齐齐哈尔", "黑龙江省", Pinyin: "qiqihaer"),

        // Yunnan & Guizhou
        new("昆明", "云南省", Pinyin: "kunming"),
        new("大理", "云南省", Pinyin: "dali"),
        new("丽江", "云南省", Pinyin: "lijiang"),
        new("西双版纳", "云南省", Pinyin: "xishuangbanna"),
        new("贵阳", "贵州省", Pinyin: "guiyang"),
        new("遵义", "贵州省", Pinyin: "zunyi"),

        // Gansu, Qinghai, Ningxia, Xinjiang, Tibet, Inner Mongolia
        new("兰州", "甘肃省", Pinyin: "lanzhou"),
        new("陇南", "甘肃省", Pinyin: "longnan"),
        new("西宁", "青海省", Pinyin: "xining"),
        new("银川", "宁夏回族自治区", Pinyin: "yinchuan"),
        new("乌鲁木齐", "新疆维吾尔自治区", Pinyin: "wulumuqi"),
        new("拉萨", "西藏自治区", Pinyin: "lasa"),
        new("呼和浩特", "内蒙古自治区", Pinyin: "huhehaote"),
        new("包头", "内蒙古自治区", Pinyin: "baotou"),

        // Hainan
        new("海口", "海南省", Pinyin: "haikou"),
        new("三亚", "海南省", Pinyin: "sanya"),
        new("海南", "海南省", Pinyin: "hainan"),

        // HK, Macao, Taiwan
        new("香港", "特别行政区", Pinyin: "xianggang"),
        new("澳门", "特别行政区", Pinyin: "aomen"),
        new("台北", "台湾省", Pinyin: "taibei"),
        new("高雄", "台湾省", Pinyin: "gaoxiong"),
        new("台南", "台湾省", Pinyin: "tainan"),
        new("台中", "台湾省", Pinyin: "taizhong"),

        // Global
        new("东京", "日本", Country: "日本", Pinyin: "dongjing,tokyo"),
        new("首尔", "韩国", Country: "韩国", Pinyin: "shouer,seoul"),
        new("新加坡", "新加坡", Country: "新加坡", Pinyin: "xinjiapo,singapore"),
        new("伦敦", "英国", Country: "英国", Pinyin: "lundun,london"),
        new("巴黎", "法国", Country: "法国", Pinyin: "bali,paris"),
        new("纽约", "美国", Country: "美国", Pinyin: "niuyue,newyork"),
        new("旧金山", "美国", Country: "美国", Pinyin: "jiujinshan,sanfrancisco"),
        new("洛杉矶", "美国", Country: "美国", Pinyin: "luoshanji,losangeles"),
        new("悉尼", "澳大利亚", Country: "澳大利亚", Pinyin: "xini,sydney"),
        new("多伦多", "加拿大", Country: "加拿大", Pinyin: "duolunduo,toronto")
    ];

    private static readonly IReadOnlyList<WeatherCityItem> DefaultPopularCities =
    [
        new("北京", "北京市", Pinyin: "beijing"),
        new("上海", "上海市", Pinyin: "shanghai"),
        new("广州", "广东省", Pinyin: "guangzhou"),
        new("深圳", "广东省", Pinyin: "shenzhen"),
        new("杭州", "浙江省", Pinyin: "hangzhou"),
        new("成都", "四川省", Pinyin: "chengdu"),
        new("武汉", "湖北省", Pinyin: "wuhan"),
        new("南京", "江苏省", Pinyin: "nanjing")
    ];

    private static readonly Dictionary<char, char> CharToInitial = new()
    {
        ['北'] = 'b', ['京'] = 'j', ['上'] = 's', ['海'] = 'h', ['天'] = 't', ['津'] = 'j',
        ['重'] = 'c', ['庆'] = 'q', ['南'] = 'n', ['苏'] = 's', ['州'] = 'z', ['无'] = 'w',
        ['锡'] = 'x', ['常'] = 'c', ['通'] = 't', ['扬'] = 'y', ['徐'] = 'x', ['盐'] = 'y',
        ['城'] = 'c', ['镇'] = 'z', ['江'] = 'j', ['泰'] = 't', ['宿'] = 's', ['迁'] = 'q',
        ['连'] = 'l', ['云'] = 'y', ['港'] = 'g', ['淮'] = 'h', ['安'] = 'a', ['昌'] = 'c',
        ['九'] = 'j', ['赣'] = 'g', ['吉'] = 'j', ['宜'] = 'y', ['春'] = 'c', ['抚'] = 'f',
        ['饶'] = 'r', ['景'] = 'j', ['德'] = 'd', ['萍'] = 'p', ['乡'] = 'x', ['新'] = 'x',
        ['余'] = 'y', ['鹰'] = 'y', ['潭'] = 't', ['宁'] = 'n', ['桂'] = 'g', ['林'] = 'l',
        ['柳'] = 'l', ['梧'] = 'w', ['玉'] = 'y', ['色'] = 's', ['郑'] = 'z',
        ['洛'] = 'l', ['阳'] = 'y', ['开'] = 'k', ['封'] = 'f', ['焦'] = 'j', ['作'] = 'z',
        ['许'] = 'x', ['平'] = 'p', ['顶'] = 'd', ['山'] = 's', ['信'] = 'x', ['商'] = 's',
        ['丘'] = 'q', ['周'] = 'z', ['口'] = 'k', ['驻'] = 'z', ['马'] = 'm', ['店'] = 'd',
        ['成'] = 'c', ['都'] = 'd', ['绵'] = 'm', ['宾'] = 'b', ['充'] = 'c', ['泸'] = 'l',
        ['达'] = 'd', ['乐'] = 'l', ['福'] = 'f', ['厦'] = 'x', ['门'] = 'm', ['泉'] = 'q',
        ['漳'] = 'z', ['莆'] = 'p', ['田'] = 't', ['龙'] = 'l', ['岩'] = 'y', ['三'] = 's',
        ['明'] = 'm', ['济'] = 'j', ['青'] = 'q', ['岛'] = 'd', ['烟'] = 'y', ['台'] = 't',
        ['潍'] = 'w', ['坊'] = 'f', ['威'] = 'w', ['淄'] = 'z', ['博'] = 'b', ['临'] = 'l',
        ['沂'] = 'y', ['合'] = 'h', ['肥'] = 'f', ['芜'] = 'w', ['湖'] = 'h', ['蚌'] = 'b',
        ['埠'] = 'b', ['铜'] = 't', ['陵'] = 'l', ['黄'] = 'h', ['滁'] = 'c',
        ['阜'] = 'f', ['六'] = 'l', ['亳'] = 'b', ['池'] = 'c', ['宣'] = 'x', ['杭'] = 'h',
        ['波'] = 'b', ['温'] = 'w', ['嘉'] = 'j', ['兴'] = 'x', ['绍'] = 's', ['金'] = 'j',
        ['华'] = 'h', ['衢'] = 'q', ['舟'] = 'z', ['丽'] = 'l', ['水'] = 's', ['广'] = 'g',
        ['深'] = 's', ['圳'] = 'z', ['珠'] = 'z', ['佛'] = 'f', ['东'] = 'd', ['莞'] = 'g',
        ['中'] = 'z', ['惠'] = 'h', ['武'] = 'w', ['汉'] = 'h', ['长'] = 'c', ['沙'] = 's',
        ['西'] = 'x', ['太'] = 't', ['原'] = 'y', ['石'] = 's', ['庄'] = 'z', ['哈'] = 'h',
        ['尔'] = 'e', ['滨'] = 'b', ['沈'] = 's', ['昆'] = 'k', ['贵'] = 'g', ['乌'] = 'w',
        ['鲁'] = 'l', ['木'] = 'm', ['齐'] = 'q', ['兰'] = 'l', ['银'] = 'y', ['川'] = 'c',
        ['呼'] = 'h', ['浩'] = 'h', ['特'] = 't', ['拉'] = 'l', ['萨'] = 's',
        ['香'] = 'x', ['澳'] = 'a', ['大'] = 'd', ['连'] = 'l',
        ['保'] = 'b', ['定'] = 'd', ['邯'] = 'h', ['郸'] = 'd', ['承'] = 'c', ['张'] = 'z',
        ['家'] = 'j', ['廊'] = 'l', ['衡'] = 'h', ['邢'] = 'x'
    };

    public static string GetAcronym(string name)
    {
        var chars = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            chars[i] = CharToInitial.TryGetValue(name[i], out var initial)
                ? initial
                : char.ToLowerInvariant(name[i]);
        }
        return new string(chars);
    }

    public static IReadOnlyList<WeatherCityItem> Search(string query, int max = 8)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return DefaultPopularCities.Take(max).ToList();
        }

        var q = query.Trim().ToLowerInvariant();

        return Database
            .Where(c => c.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                     || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || c.Pinyin.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                     || c.Pinyin.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || GetAcronym(c.Name).StartsWith(q, StringComparison.OrdinalIgnoreCase)
                     || GetAcronym(c.Name).Contains(q, StringComparison.OrdinalIgnoreCase)
                     || c.Admin.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Name.Equals(q, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(c => c.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(c => GetAcronym(c.Name).Equals(q, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(c => c.Pinyin.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .ToList();
    }
}
