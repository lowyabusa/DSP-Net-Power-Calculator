# DSP Net-Energy Calculator（戴森球计划净发电计算器）

[English](README.md) | [Deutsch](README.de.md) | [简体中文](README.zh-CN.md)

> 本项目通过 AI「氛围编程」（vibe coding）开发。

基于 .NET 10 Blazor WebAssembly 的《戴森球计划》燃料生产链净发电计算器。C# 计算引擎在浏览器本地运行，支持原版游戏中火力发电厂、微型聚变发电站和人造恒星可使用的全部 24 种燃料。物品、配方、设备、发电设施及增产剂规则分别保存在独立的 JSON 数据集中。

与通用生产计算器不同，本计算器会扣除生产链自身的耗电量，计算最终可用的**净发电功率**，涵盖资源开采、增产剂生产以及已配置的物流耗电。所选计算范围之外的成本会明确标为未计入。

## 无需安装 .NET 即可使用网站

将计算器部署到静态网站托管服务后，在浏览器中打开其网址。从本仓库部署网站的方法见[开发：构建与发布](#开发构建与发布)。**使用者无需安装 .NET、.NET SDK 或桌面应用。** 首次访问时，浏览器会自动下载应用及所需的 WebAssembly 运行时。全部计算在浏览器本地完成，无需计算后端服务。

在浏览器存储可用时，方案、语言和导入的图标保存在当前浏览器配置文件中，并与该网站地址关联；清除网站数据也会删除这些设置和图标。可通过 JSON 导出备份方案。

## 离线使用

已发布的网站通过 Service Worker 缓存应用文件，此功能需要 HTTPS 或 localhost。文件缓存完成且 Service Worker 已激活后，在同一浏览器配置文件中访问同一网站地址，即可使用已缓存的运行时离线重新加载和使用计算器，也无需单独安装 .NET。离线缓存必须保留；清除网站数据也会删除该缓存。下文介绍的开发服务器不提供此离线缓存。

## 使用方法

- 首次使用默认显示英文。右上角语言按钮可切换英文、德文和简体中文；语言偏好与方案分别保存。
- 左侧选择燃料及增产剂等级，中间输入目标燃料产量；右侧显示总发电功率、耗电量、净功率及计算范围。
- **All fuels** 打开按发电设施分类的可搜索燃料选择器。木材、植物燃料和能量碎片按外部供应物品处理，其采集或获取成本标为未计入。
- 点击配方、材料和设备图标可选择替代方案。右键点击生产步骤，或使用该步骤的操作按钮，可修改设备数量、利用率及功率。支持增产时，可通过喷涂图标选择额外产出或生产加速；不可用选项的原因会显示在提示中。
- 煤炭与高能石墨的参考比较以每分钟 120 个外部供应煤炭为基准，不计资源开采、分拣器及物流耗电。增产剂比较使用当前方案。展开物料流可查看各步骤的吞吐量、工作功率和待机功率。
- 方案顶部的 **Import JSON** 和 **Export JSON** 用于导入和导出配方、设备、喷涂及物流设置。方案文件使用架构版本 1。导入失败时保留当前方案。
- **Alternative recipe chains** 在相同燃料产量下按净功率排列可行配方组合，并计入上游增产剂生产。发电设施上限、外部供应材料、工厂设置及物流参数保持一致。列表标出外部供应材料并统计不可行组合；选择 **Apply** 可应用对应生产链。
- 有效方案的修改会自动保存在浏览器中。在网址后添加 `?transient=1`，例如 `http://localhost:5080/?transient=1`，可启动英文默认会话，不读取或保存已有方案、语言和图标。

以上英文按钮名称对应英文界面。

## 游戏图标

在 Steam 中右键点击《戴森球计划》，选择**管理 > 浏览本地文件**。在计算器中选择 **Game icons > Choose game folder**，选取包含 `DSPGAME.exe` 和 `DSPGAME_Data` 的游戏安装目录。支持自定义 Steam 库位置。浏览器需要手动选择目录；**Steam** 按钮用于打开游戏的 Steam 库页面。

选择目录后，浏览器仅在本地读取 `DSPGAME_Data/resources.assets` 和 `DSPGAME_Data/sharedassets0.assets`，提取支持的图标。解码后的图标保存在浏览器的 IndexedDB 中，供后续访问及离线使用。游戏文件和图标不会上传。选择 **Continue without icons** 可使用文字标签；可随时通过顶部的 **Game icons** 重新打开设置。缺失的图标会以文字替代。

图标读取器针对 DSP 0.10.35.29151 和 Unity 2022.3.62 的 Windows 游戏资源进行了检查。游戏美术资源不随仓库或发布的网站分发，版权归相应权利人所有。本项目为独立的爱好者工具，使用的 [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) 遵循其 [MIT 许可证](src/DspNetEnergy.Desktop/Assets/AssetsTools.NET.LICENSE.txt)。

## 物流耗电

在高级方案设置中打开物流成本编辑。分拣器和物流运输站可设置数量、平均利用率、单台工作及待机功率。运输站功率仅表示站体运行耗电，不含运输工具充电；无人机和运输船的充电耗电需分别填写。物流无人机和星际物流运输船的平均充电功率（MW）为每分钟往返次数乘以每次往返能耗（kJ），再除以 60,000；往返能耗必须包含回程。

留空的类别标为未计入；输入数量或频率为零，可明确计入未使用的类别。配置分项物流成本后，基础设施总功率仅表示额外成本。配方比较对每条生产链使用相同的物流参数。

## 开发：从源代码运行

**仅构建源代码、启动开发服务器和运行 .NET 测试时需要 .NET 10 SDK。** 使用已发布的网站无需安装 SDK。

安装 SDK 后，在项目目录打开 PowerShell 并执行：

```powershell
dotnet run --project src/DspNetEnergy.Web --configuration Release -- --urls http://localhost:5080
```

在浏览器中打开 [http://localhost:5080/](http://localhost:5080/)。使用此开发服务器期间，请保持命令运行。

## 开发：构建与发布

安装 .NET 10 SDK 后，生成静态网站：

```powershell
dotnet publish src/DspNetEnergy.Web --configuration Release
```

通过静态 Web 服务器提供 `src/DspNetEnergy.Web/bin/Release/net10.0/publish/wwwroot` 目录中的文件。此静态 Web 服务器无需安装 .NET。使用 HTTPS 或 localhost，并通过以 `/` 结尾的网址访问。不能直接从文件系统打开 `index.html`。

仓库包含需手动触发的 **Publish calculator to GitHub Pages** 工作流。配置及浏览器验证命令见 [浏览器版本说明（英文）](docs/blazor-web.md)。

## 测试

在 Windows 上安装 .NET 10 SDK，并在项目目录执行；解决方案也包含 WPF 桌面应用：

```powershell
dotnet test DspNetEnergy.slnx --configuration Release
```

浏览器与原生 .NET 的结果一致性、本地存储、JSON 导入导出、离线重新加载及可选的本地游戏图标导入测试，见 [浏览器验证说明（英文）](docs/blazor-web.md#verification)。

## 计算引擎与游戏数据

C# 使用示例、计算边界、喷涂计费和数据来源的详细说明见 [英文 README](README.md#calculating-a-production-chain) 或 [德文 README](README.de.md#eine-produktionskette-berechnen)。

配方目录基于 MIT 许可的 [FactorioLab 数据集](https://github.com/factoriolab/factoriolab/blob/main/public/data/dsp/data.json)，数据标记为 `version.DSP = 0.10.29.21950`；项目记录的游戏交叉检查版本为 **0.10.35.29151**（Steam 构建 25955035）。24 种燃料包括 21 种化学燃料、1 种聚变燃料和 2 种反物质燃料。充满电的蓄电器用于能量枢纽，透镜用于射线接收站。物品和配方名称来自该游戏版本的英文、德文及简体中文本地化表。[FactorioLab MIT 许可证](src/DspNetEnergy.Core/GameData/FactorioLab.LICENSE.txt)
