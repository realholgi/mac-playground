using System;
using System.Drawing;
using System.Windows.Forms;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class DataGridViewValidationTests
{
    [Test]
    public void CancelledValidationSeesRawInvalidTextBeforeParsing()
    {
        using var form = new Form { ClientSize = new Size(300, 180) };
        using var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        grid.Columns.Add(new DataGridViewTextBoxColumn { ValueType = typeof(int) });
        grid.Rows.Add();
        grid[0, 0].Value = 12;
        form.Controls.Add(grid);
        try
        {
            form.Show();
            Application.DoEvents();
            grid.CurrentCell = grid[0, 0];
            Assert.That(grid.BeginEdit(true), Is.True);
            ((TextBox)grid.EditingControl).Text = "not a number";
            object observed = null;
            int validated = 0;
            grid.CellValidating += (_, e) => { observed = e.FormattedValue; e.Cancel = true; };
            grid.CellValidated += (_, _) => validated++;

            Assert.That(grid.EndEdit(), Is.False);
            Assert.That(observed, Is.EqualTo("not a number"));
            Assert.That(validated, Is.Zero);
            Assert.That(grid[0, 0].Value, Is.EqualTo(12));
            Assert.That(grid.CurrentCell.IsInEditMode, Is.True);
        }
        finally { form.Close(); }
    }

    [Test]
    public void AcceptedValidationParsesAndCommitsAfterCallback()
    {
        using var form = new Form { ClientSize = new Size(300, 180) };
        using var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        grid.Columns.Add(new DataGridViewTextBoxColumn { ValueType = typeof(int) });
        grid.Rows.Add();
        grid[0, 0].Value = 12;
        form.Controls.Add(grid);
        try
        {
            form.Show();
            Application.DoEvents();
            grid.CurrentCell = grid[0, 0];
            Assert.That(grid.BeginEdit(true), Is.True);
            ((TextBox)grid.EditingControl).Text = "42";
            object observed = null;
            object valueDuringValidation = null;
            grid.CellValidating += (_, e) => {
                observed = e.FormattedValue;
                valueDuringValidation = grid[0, 0].Value;
            };

            Assert.That(grid.EndEdit(), Is.True);
            Assert.That(observed, Is.EqualTo("42"));
            Assert.That(valueDuringValidation, Is.EqualTo(12));
            Assert.That(grid[0, 0].Value, Is.EqualTo(42));
        }
        finally { form.Close(); }
    }
}
