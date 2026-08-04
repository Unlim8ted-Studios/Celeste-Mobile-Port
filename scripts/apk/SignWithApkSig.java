import com.android.apksig.ApkSigner;
import java.io.File;
import java.io.FileInputStream;
import java.security.Key;
import java.security.KeyStore;
import java.security.PrivateKey;
import java.security.cert.Certificate;
import java.security.cert.X509Certificate;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public class SignWithApkSig {
    public static void main(String[] args) throws Exception {
        if (args.length != 7) {
            throw new IllegalArgumentException("usage: <in.apk> <out.apk> <keystore> <storepass> <alias> <keypass> <minSdk>");
        }

        File inputApk = new File(args[0]);
        File outputApk = new File(args[1]);
        File keystoreFile = new File(args[2]);
        char[] storePass = args[3].toCharArray();
        String alias = args[4];
        char[] keyPass = args[5].toCharArray();
        int minSdk = Integer.parseInt(args[6]);

        KeyStore keyStore = KeyStore.getInstance(KeyStore.getDefaultType());
        try (FileInputStream in = new FileInputStream(keystoreFile)) {
            keyStore.load(in, storePass);
        }

        Key key = keyStore.getKey(alias, keyPass);
        if (!(key instanceof PrivateKey)) {
            throw new IllegalStateException("Alias does not contain a private key: " + alias);
        }

        Certificate[] chain = keyStore.getCertificateChain(alias);
        if (chain == null || chain.length == 0) {
            throw new IllegalStateException("Alias does not contain a certificate chain: " + alias);
        }

        List<X509Certificate> certs = new ArrayList<>();
        for (Certificate cert : chain) {
            certs.add((X509Certificate) cert);
        }

        ApkSigner.SignerConfig signerConfig =
                new ApkSigner.SignerConfig.Builder("CELESTE", (PrivateKey) key, certs).build();

        new ApkSigner.Builder(Collections.singletonList(signerConfig))
                .setInputApk(inputApk)
                .setOutputApk(outputApk)
                .setMinSdkVersion(minSdk)
                .setV1SigningEnabled(true)
                .setV2SigningEnabled(true)
                .setV3SigningEnabled(true)
                .build()
                .sign();
    }
}
