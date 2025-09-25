using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace WindowsFormsApp3
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void partnersBindingNavigatorSaveItem_Click(object sender, EventArgs e)
		{
			this.Validate();
			this.partnersBindingSource.EndEdit();
			this.tableAdapterManager.UpdateAll(this.cleanPlanetFatkulinDataSet);

		}

		private void Form1_Load(object sender, EventArgs e)
		{
			// TODO: данная строка кода позволяет загрузить данные в таблицу "cleanPlanetFatkulinDataSet.Partners". При необходимости она может быть перемещена или удалена.
			this.partnersTableAdapter.Fill(this.cleanPlanetFatkulinDataSet.Partners);

			ApplyTheme();

		}

		private void ApplyTheme()
		{
			string fontName = "Franklin Gothic Medium";
			Font baseFont = new Font(fontName, this.Font.Size, this.Font.Style);
			this.Font = baseFont;
			ApplyThemeToControl(this, baseFont);
		}

		private void ApplyThemeToControl(Control parent, Font font)
		{
			foreach (Control control in parent.Controls)
			{
				// Устанавливаем шрифт
				control.Font = new Font(font, control.Font.Style);

				// Кнопки — акцентный цвет #00CED1
				if (control is Button btn)
				{
					btn.BackColor = ColorTranslator.FromHtml("#00CED1");
					btn.UseVisualStyleBackColor = false;
					btn.ForeColor = Color.Black;
				}

				// DataGridView — применяем шрифт к ячейкам и заголовкам
				if (control is DataGridView grid)
				{
					grid.Font = new Font(font, grid.Font.Style);
					grid.DefaultCellStyle.Font = new Font(font, grid.DefaultCellStyle.Font.Style);
					grid.ColumnHeadersDefaultCellStyle.Font = new Font(font, grid.ColumnHeadersDefaultCellStyle.Font.Style);
				}

				// ToolStrip/BindingNavigator — собственный шрифт
				if (control is ToolStrip strip)
				{
					strip.Font = new Font(font, strip.Font.Style);
				}

				// Рекурсивно для дочерних
				if (control.HasChildren)
				{
					ApplyThemeToControl(control, font);
				}
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			string connectionString = Properties.Settings.Default.CleanPlanetFatkulinConnectionString;
			List<string> partnerCards = new List<string>();

			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();

					string queryPartners = @"
SELECT 
    p.PartnerId,
    p.PartnerName,
    p.PartnerTypeId,
    p.DirectorFullName,
    p.Phone,
    p.Rating
FROM dbo.Partners p";

					using (SqlCommand cmd = new SqlCommand(queryPartners, connection))
					{
						using (SqlDataReader reader = cmd.ExecuteReader())
						{
							while (reader.Read())
							{
								int partnerID = Convert.ToInt32(reader["PartnerId"]);
								string companyName = reader["PartnerName"].ToString();
								int partnerTypeId = Convert.ToInt32(reader["PartnerTypeId"]);
								string directorName = reader["DirectorFullName"].ToString();
								string phone = reader["Phone"].ToString();
								int rating = Convert.ToInt32(reader["Rating"]);

								string typeNameOrId = $"Тип ID: {partnerTypeId}";

								string card = $@"
Тип | Наименование партнера
{typeNameOrId} | {companyName}
Директор: {directorName}
Телефон: {phone}
Рейтинг: {rating}";

								partnerCards.Add(card.Trim());
							}
						}
					}
				}

				string message = string.Join("\n\n", partnerCards);
				MessageBox.Show(message, "Список партнёров", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (SqlException ex)
			{
				MessageBox.Show($"Ошибка подключения к базе данных: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private decimal CalculateServiceCost(int serviceId)
		{
			string connectionString = Properties.Settings.Default.CleanPlanetFatkulinConnectionString;
			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();

					// 1) Материалы: SUM(norm.QuantityPerService * m.Price)
					string materialsSql = @"
SELECT ISNULL(SUM(n.QuantityPerService * m.Price), 0)
FROM dbo.ServiceMaterialNorms n
JOIN dbo.Materials m ON m.MaterialId = n.MaterialId
WHERE n.ServiceId = @ServiceId";

					decimal materialsCost;
					using (SqlCommand cmd = new SqlCommand(materialsSql, connection))
					{
						cmd.Parameters.AddWithValue("@ServiceId", serviceId);
						object result = cmd.ExecuteScalar();
						materialsCost = Convert.ToDecimal(result);
					}

					// 2) Трудозатраты: Services.TimeNormHours * Qualification.HourlyRate
					string laborSql = @"
SELECT ISNULL(s.TimeNormHours * q.HourlyRate, 0)
FROM dbo.Services s
JOIN dbo.Qualification q ON q.QualificationId = s.QualificationId
WHERE s.ServiceId = @ServiceId";

					decimal laborCost;
					using (SqlCommand cmd = new SqlCommand(laborSql, connection))
					{
						cmd.Parameters.AddWithValue("@ServiceId", serviceId);
						object result = cmd.ExecuteScalar();
						if (result == null || result == DBNull.Value)
							return -1m; // услуга не найдена
						laborCost = Convert.ToDecimal(result);
					}

					return materialsCost + laborCost;
				}
			}
			catch
			{
				return -1m;
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			if (!TryPickServiceIdFromList(out int serviceId)) return;
			decimal cost = CalculateServiceCost(serviceId);
			if (cost < 0)
			{
				MessageBox.Show("Услуга или связанные данные не найдены", "Результат", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			else
			{
				MessageBox.Show($"Себестоимость услуги (ID={serviceId}): {cost:F2}", "Результат", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		private bool TryPickServiceIdFromList(out int serviceId)
		{
			serviceId = 0;
			string connectionString = Properties.Settings.Default.CleanPlanetFatkulinConnectionString;
			DataTable table = new DataTable();
			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				using (SqlCommand cmd = new SqlCommand("SELECT ServiceId, ServiceName, TimeNormHours, QualificationId FROM dbo.Services", conn))
				using (SqlDataAdapter da = new SqlDataAdapter(cmd))
				{
					conn.Open();
					da.Fill(table);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Не удалось загрузить список услуг: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			using (Form dlg = new Form())
			using (DataGridView grid = new DataGridView())
			using (Button ok = new Button())
			using (Button cancel = new Button())
			{
				dlg.Text = "Выберите услугу";
				dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
				dlg.StartPosition = FormStartPosition.CenterParent;
				dlg.MinimizeBox = false;
				dlg.MaximizeBox = false;
				dlg.ClientSize = new Size(700, 400);

				grid.Parent = dlg;
				grid.Location = new Point(10, 10);
				grid.Size = new Size(680, 340);
				grid.ReadOnly = true;
				grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
				grid.MultiSelect = false;
				grid.AutoGenerateColumns = true;
				grid.DataSource = table;

				ok.Text = "OK";
				ok.Location = new Point(510, 360);
				ok.DialogResult = DialogResult.OK;
				cancel.Text = "Отмена";
				cancel.Location = new Point(600, 360);
				cancel.DialogResult = DialogResult.Cancel;

				dlg.AcceptButton = ok;
				dlg.CancelButton = cancel;
				dlg.Controls.Add(ok);
				dlg.Controls.Add(cancel);

				if (dlg.ShowDialog(this) == DialogResult.OK && grid.CurrentRow != null)
				{
					object val = grid.CurrentRow.Cells["ServiceId"].Value;
					if (val != null && int.TryParse(val.ToString(), out serviceId))
					{
						return true;
					}
				}
			}

			return false;
		}

		private void button3_Click(object sender, EventArgs e)
		{
			string connectionString = Properties.Settings.Default.CleanPlanetFatkulinConnectionString;
			DataTable table = new DataTable();
			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				using (SqlCommand cmd = new SqlCommand(@"
SELECT h.HistoryId,
       p.PartnerName,
       s.ServiceName,
       h.Quantity,
       h.PerformedAt
FROM dbo.PartnerServiceHistory h
JOIN dbo.Partners p ON p.PartnerId = h.PartnerId
JOIN dbo.Services s ON s.ServiceId = h.ServiceId
ORDER BY h.PerformedAt DESC", conn))
				using (SqlDataAdapter da = new SqlDataAdapter(cmd))
				{
					conn.Open();
					da.Fill(table);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Не удалось загрузить историю продаж: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			using (Form dlg = new Form())
			using (DataGridView grid = new DataGridView())
			{
				dlg.Text = "История продаж";
				dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
				dlg.StartPosition = FormStartPosition.CenterParent;
				dlg.MinimizeBox = false;
				dlg.MaximizeBox = false;
				dlg.ClientSize = new Size(900, 500);

				grid.Parent = dlg;
				grid.Dock = DockStyle.Fill;
				grid.ReadOnly = true;
				grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
				grid.MultiSelect = false;
				grid.AutoGenerateColumns = true;
				grid.DataSource = table;

				ApplyThemeToControl(dlg, this.Font);

				dlg.ShowDialog(this);
			}
		}
	}
}

