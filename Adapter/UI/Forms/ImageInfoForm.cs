using KosmosAdapterV2.Core.Models;

namespace KosmosAdapterV2.UI.Forms;

public partial class ImageInfoForm : Form
{
    public ImageInfoForm(ImageInfo imageInfo)
    {
        InitializeComponent();
        PopulateInfo(imageInfo);
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        var lblWidth = CreateLabel("Genişlik:", 10, 10);
        var txtWidth = CreateTextBox(imageInfo: null, 100, 10);
        txtWidth.Name = "txtWidth";

        var lblHeight = CreateLabel("Yükseklik:", 10, 40);
        var txtHeight = CreateTextBox(imageInfo: null, 100, 40);
        txtHeight.Name = "txtHeight";

        var lblBitsPerPixel = CreateLabel("Bit/Piksel:", 10, 70);
        var txtBitsPerPixel = CreateTextBox(imageInfo: null, 100, 70);
        txtBitsPerPixel.Name = "txtBitsPerPixel";

        var lblSize = CreateLabel("Boyut (KB):", 10, 100);
        var txtSize = CreateTextBox(imageInfo: null, 100, 100);
        txtSize.Name = "txtSize";

        var btnOK = new Button
        {
            Text = "Tamam",
            DialogResult = DialogResult.OK,
            Location = new Point(180, 140),
            Size = new Size(80, 30)
        };

        Controls.AddRange(new Control[]
        {
            lblWidth, txtWidth,
            lblHeight, txtHeight,
            lblBitsPerPixel, txtBitsPerPixel,
            lblSize, txtSize,
            btnOK
        });

        AcceptButton = btnOK;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(280, 185);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ImageInfoForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Görüntü Bilgisi";

        ResumeLayout(false);
    }

    private void PopulateInfo(ImageInfo info)
    {
        if (Controls["txtWidth"] is TextBox txtWidth)
            txtWidth.Text = info.Width.ToString();
        
        if (Controls["txtHeight"] is TextBox txtHeight)
            txtHeight.Text = info.Height.ToString();
        
        if (Controls["txtBitsPerPixel"] is TextBox txtBitsPerPixel)
            txtBitsPerPixel.Text = info.BitsPerPixel.ToString();
        
        if (Controls["txtSize"] is TextBox txtSize)
            txtSize.Text = (info.SizeInBytes / 1024).ToString();
    }

    private static Label CreateLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y + 3),
            Size = new Size(80, 20)
        };
    }

    private static TextBox CreateTextBox(ImageInfo? imageInfo, int x, int y)
    {
        return new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(160, 23),
            ReadOnly = true
        };
    }
}
