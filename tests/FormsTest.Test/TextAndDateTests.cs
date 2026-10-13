using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class TextAndDateTests
{
    [Test]
    public void SingleLineSelectionReplacesOnlySelectedTextAndClampsAtTheEnd()
    {
        using var form = new TextBoxForm();
        try
        {
            form.Show();
            AppKit.NSApplication.SharedApplication.Activate();
            form.Activate();
            Application.DoEvents();
            var textBox = (TextBox)form.Controls.Find("textbox1", true).Single();
            textBox.Focus();
            Assert.That(() =>
            {
                Application.DoEvents();
                return AppKit.NSApplication.SharedApplication.Active && textBox.Focused;
            }, Is.True.After(2000, 10), "The text box must own native focus before editing its selection.");
            textBox.Text = "alpha beta gamma";
            textBox.Select(6, 4);

            Assert.That(textBox.SelectionStart, Is.EqualTo(6));
            Assert.That(textBox.SelectionLength, Is.EqualTo(4));
            Assert.That(textBox.SelectedText, Is.EqualTo("beta"));

            textBox.SelectedText = "B";
            Application.DoEvents();
            Assert.That(textBox.Text, Is.EqualTo("alpha B gamma"));

            textBox.SelectionStart = textBox.Text.Length + 5;
            textBox.SelectionLength = 20;
            Assert.That(textBox.SelectionStart, Is.EqualTo(13));
            Assert.That(textBox.SelectionLength, Is.Zero);
            Assert.That(textBox.SelectedText, Is.Empty);

            textBox.SelectedText = "!";
            Application.DoEvents();
            Assert.That(textBox.Text, Is.EqualTo("alpha B gamma!"));
        }
        finally
        {
            form.Close();
        }
    }

    [Test]
    public void MultilineAppendAndReplacementPreserveUneditedLines()
    {
        using var form = new TextBoxForm();
        try
        {
            form.Show();
            Application.DoEvents();
            var textBox = (TextBox)form.Controls.Find("textbox4", true).Single();
            textBox.Text = "first\nsecond";
            textBox.Select(0, 5);
            textBox.AppendText("\nthird");
            Application.DoEvents();

            Assert.That(textBox.Text, Is.EqualTo("first\nsecond\nthird"));
            Assert.That(textBox.Lines, Is.EqualTo(new[] { "first", "second", "third" }));

            textBox.Select(6, 6);
            Assert.That(textBox.SelectedText, Is.EqualTo("second"));
            textBox.SelectedText = "middle\nextra";
            Application.DoEvents();

            Assert.That(textBox.Text, Is.EqualTo("first\nmiddle\nextra\nthird"));
            Assert.That(textBox.Lines, Is.EqualTo(new[] { "first", "middle", "extra", "third" }));

            textBox.SelectAll();
            Assert.That(textBox.SelectedText, Is.EqualTo(textBox.Text));
            textBox.SelectedText = "replacement";
            Application.DoEvents();
            Assert.That(textBox.Text, Is.EqualTo("replacement"));
            Assert.That(textBox.Lines, Is.EqualTo(new[] { "replacement" }));
        }
        finally
        {
            form.Close();
        }
    }

    [Test]
    public void RichTextSelectionReplacesTheSelectedWordAndCanInsertAtTheEnd()
    {
        using var form = new TextBoxForm();
        try
        {
            form.Show();
            Application.DoEvents();
            var textBox = (RichTextBox)form.Controls.Find("textbox5", true).Single();
            textBox.Text = "red green blue";
            textBox.Select(4, 5);

            Assert.That(textBox.SelectionStart, Is.EqualTo(4));
            Assert.That(textBox.SelectionLength, Is.EqualTo(5));
            Assert.That(textBox.SelectedText, Is.EqualTo("green"));

            textBox.SelectedText = "gold";
            Application.DoEvents();
            Assert.That(textBox.Text, Is.EqualTo("red gold blue"));

            textBox.Select(textBox.Text.Length, 0);
            Assert.That(textBox.SelectedText, Is.Empty);
            textBox.SelectedText = "!";
            Application.DoEvents();
            Assert.That(textBox.Text, Is.EqualTo("red gold blue!"));
        }
        finally
        {
            form.Close();
        }
    }

    [Test]
    public void DatePickerPublishesChangedDatesButNotRepeatedAssignments()
    {
        using var form = new DateTimeForm();
        try
        {
            form.Show();
            Application.DoEvents();
            var picker = Descendants(form).OfType<DateTimePicker>().Single();
            var initial = new DateTime(2024, 2, 28);
            var leapDay = new DateTime(2024, 2, 29);
            var nextMonth = new DateTime(2024, 3, 1);
            picker.Value = initial;
            Application.DoEvents();
            Assert.That(picker.Value, Is.EqualTo(initial));

            var observed = new List<DateTime>();
            picker.ValueChanged += (_, _) => observed.Add(picker.Value);
            picker.Value = leapDay;
            Application.DoEvents();
            Assert.That(picker.Value, Is.EqualTo(leapDay));
            Assert.That(observed, Is.EqualTo(new[] { leapDay }));

            picker.Value = leapDay;
            Application.DoEvents();
            Assert.That(picker.Value, Is.EqualTo(leapDay));
            Assert.That(observed, Is.EqualTo(new[] { leapDay }));

            picker.Value = nextMonth;
            Application.DoEvents();
            Assert.That(picker.Value, Is.EqualTo(nextMonth));
            Assert.That(observed, Is.EqualTo(new[] { leapDay, nextMonth }));
        }
        finally
        {
            form.Close();
        }
    }

    [Test]
    public void CalendarRangeHonorsTheInclusiveMaximumAndSetDateCollapsesIt()
    {
        using var form = new DateTimeForm();
        try
        {
            form.Show();
            Application.DoEvents();
            var calendar = Descendants(form).OfType<MonthCalendar>().Single();
            var start = new DateTime(2024, 2, 20);

            calendar.SetSelectionRange(start, start.AddDays(9));
            Application.DoEvents();
            Assert.That(calendar.SelectionStart, Is.EqualTo(start));
            Assert.That(calendar.SelectionEnd, Is.EqualTo(new DateTime(2024, 2, 29)));
            Assert.That((calendar.SelectionEnd - calendar.SelectionStart).Days + 1, Is.EqualTo(10));

            calendar.SelectionEnd = new DateTime(2024, 3, 1);
            Application.DoEvents();
            Assert.That(calendar.SelectionStart, Is.EqualTo(new DateTime(2024, 2, 21)));
            Assert.That(calendar.SelectionEnd, Is.EqualTo(new DateTime(2024, 3, 1)));
            Assert.That((calendar.SelectionEnd - calendar.SelectionStart).Days + 1, Is.EqualTo(10));

            calendar.SetSelectionRange(start, new DateTime(2024, 3, 3));
            Application.DoEvents();
            Assert.That(calendar.SelectionStart, Is.EqualTo(new DateTime(2024, 2, 23)));
            Assert.That(calendar.SelectionEnd, Is.EqualTo(new DateTime(2024, 3, 3)));
            Assert.That((calendar.SelectionEnd - calendar.SelectionStart).Days + 1, Is.EqualTo(10));

            var selected = new DateTime(2024, 2, 29);
            calendar.SetDate(selected);
            Application.DoEvents();
            Assert.That(calendar.SelectionStart, Is.EqualTo(selected));
            Assert.That(calendar.SelectionEnd, Is.EqualTo(selected));
            Assert.That(calendar.SelectionRange.Start, Is.EqualTo(selected));
            Assert.That(calendar.SelectionRange.End, Is.EqualTo(selected));
        }
        finally
        {
            form.Close();
        }
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
