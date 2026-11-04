using iText.Bouncycastle.Crypto;
using iText.Bouncycastle.X509;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

// Ephemeral self-signed test identity. No personal certificate, private-key file or network.
static class SignatureFixture
{
    public static void Create(string input, string output)
    {
        var generator = new RsaKeyPairGenerator();
        generator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
        var pair = generator.GenerateKeyPair();
        var certificate = new X509V3CertificateGenerator();
        var name = new X509Name("CN=EditorPDF local test only");
        certificate.SetSerialNumber(BigInteger.One);
        certificate.SetIssuerDN(name);
        certificate.SetSubjectDN(name);
        certificate.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        certificate.SetNotAfter(DateTime.UtcNow.AddDays(1));
        certificate.SetPublicKey(pair.Public);
        var signedCertificate = certificate.Generate(new Asn1SignatureFactory("SHA256WITHRSA", pair.Private));
        using var reader = new PdfReader(input);
        using var stream = File.Create(output);
        var signer = new PdfSigner(reader, stream, new StampingProperties());
        signer.GetSignerProperties().SetPageRect(new iText.Kernel.Geom.Rectangle(50, 100, 200, 50));
        signer.SignDetached(new PrivateKeySignature(new PrivateKeyBC(pair.Private), "SHA-256"), [new X509CertificateBC(signedCertificate)], null, null, null, 0, PdfSigner.CryptoStandard.CADES);
    }
    public static bool Verify(string path)
    {
        using var document = new iText.Kernel.Pdf.PdfDocument(new PdfReader(path));
        var signatures = new SignatureUtil(document);
        var names = signatures.GetSignatureNames();
        return names.Count == 1 && signatures.SignatureCoversWholeDocument(names[0]) && signatures.ReadSignatureData(names[0]).VerifySignatureIntegrityAndAuthenticity();
    }
}
