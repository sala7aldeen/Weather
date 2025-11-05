<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="default.aspx.cs" Inherits="genicalgofinal._default" EnableSessionState="False" %>
<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>GA - توقعات الطقس (بيانات حقيقية)</title>
    <link href="https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        body { padding: 20px; background-color: #f4f7f6; }
        .container { max-width: 1200px; }
        .card { margin-bottom: 20px; }
        .card-header { background-color: #007bff; color: white; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager1" runat="server"></asp:ScriptManager>
        <div class="container">
            <h2>التنبؤ بالطقس (GA) - بيانات حقيقية (15k صف)</h2>
            <hr />

            <div class="row">
                <div class="col-md-4">
                    <div class="card">
                        <div class="card-header">إعدادات الخوارزمية (GA Parameters)</div>
                        <div class="card-body">
                            <div class="form-group">
                                <label>حجم السكان (Population Size):</label>
                                <asp:TextBox ID="TextBox1" runat="server" CssClass="form-control">100</asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label>معدل التزاوج (Crossover Rate %):</label>
                                <asp:TextBox ID="TextBox3" runat="server" CssClass="form-control">80</asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label>معدل الطفرة (Mutation Rate %):</label>
                                <asp:TextBox ID="TextBox4" runat="server" CssClass="form-control">10</asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label>الجيل الأقصى (Max Generations):</label>
                                <asp:TextBox ID="TextBox5" runat="server" CssClass="form-control">100</asp:TextBox>
                            </div>
                            <asp:Button ID="Button1" runat="server" Text="بدء التدريب (Run)" OnClick="Button1_Click" CssClass="btn btn-primary btn-block" />
                        </div>
                    </div>
                </div>

                <div class="col-md-8">
                    <div class="card">
                        <div class="card-header">النتائج والتحليل</div>
                        <div class="card-body">
                            <canvas id="fitnessChart"></canvas>
                            <hr />
                            <h4>أفضل نتيجة تم التوصل إليها:</h4>
                            <p><strong>أفضل لياقة (Fitness):</strong> <asp:Label ID="lblBestFitness" runat="server" Text="N/A"></asp:Label></p>
                            <p><strong>متوسط الخطأ (MSE):</strong> <asp:Label ID="lblBestMSE" runat="server" Text="N/A"></asp:Label></p>
                            <p><strong>أفضل نموذج (الأوزان):</strong> <asp:Label ID="lblBestModel" runat="server" Text="N/A" Font-Size="Small"></asp:Label></p>
                        </div>
                    </div>
                </div>
            </div>

            <asp:HiddenField ID="hfChartData" runat="server" />
        </div>
    </form>

    <script type="text/javascript">
        // ترسخ وظيفة الرسم: تُنفّذ عندما يكون لدى الصفحة JSON في الحقل المخفي
        function drawChart(labels, bestData, avgData) {
            var ctx = document.getElementById('fitnessChart').getContext('2d');

            if (window.myFitnessChart) {
                window.myFitnessChart.destroy();
            }

            window.myFitnessChart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [
                        {
                            label: 'أفضل لياقة (Best Fitness)',
                            data: bestData,
                            borderColor: 'blue',
                            fill: false
                        },
                        {
                            label: 'متوسط اللياقة (Avg Fitness)',
                            data: avgData,
                            borderColor: 'gray',
                            fill: false
                        }
                    ]
                },
                options: {
                    responsive: true,
                    title: { display: true, text: 'تطور اللياقة عبر الأجيال' }
                }
            });
        }

        // قراءة hfChartData وتنفيذ drawChart إذا وُجدت بيانات
        function InitChartFromHidden() {
            var hf = document.getElementById('<%= hfChartData.ClientID %>');
            if (!hf) return;

            var chartDataJson = hf.value;
            if (chartDataJson && chartDataJson.trim() !== "") {
                try {
                    var chartData = JSON.parse(chartDataJson);
                    drawChart(chartData.generations || [], chartData.bestFitness || [], chartData.avgFitness || []);
                    return;
                } catch (e) {
                    console && console.error('Failed to parse chart JSON:', e);
                }
            }

            // رسم مخطط فارغ إذا لا توجد بيانات أو فشل التحليل
            drawChart([], [], []);
        }

        // EndRequestHandler لطلبات partial postback (UpdatePanel / async postbacks)
        function EndRequestHandler(sender, args) {
            // عند انتهاء الطلب الجزئي نُعيد تهيئة الرسم من الحقل المخفي
            InitChartFromHidden();
        }

        if (typeof (Sys) !== 'undefined' && Sys.WebForms && Sys.WebForms.PageRequestManager) {
            Sys.WebForms.PageRequestManager.getInstance().add_endRequest(EndRequestHandler);
        }

        // استدعاء عند تحميل الصفحة (full postback أو أول تحميل)
        window.addEventListener('load', InitChartFromHidden);
    </script>
</body>
</html>