import com.android.apksig.ApkVerifier;
import java.io.File;

public class VerifyWithApkSig {
    public static void main(String[] args) throws Exception {
        ApkVerifier.Result result = new ApkVerifier.Builder(new File(args[0])).build().verify();
        System.out.println("verified=" + result.isVerified());
        System.out.println("v1=" + result.isVerifiedUsingV1Scheme());
        System.out.println("v2=" + result.isVerifiedUsingV2Scheme());
        System.out.println("v3=" + result.isVerifiedUsingV3Scheme());
        if (!result.getErrors().isEmpty()) {
            System.out.println("errors=" + result.getErrors());
        }
        if (!result.getWarnings().isEmpty()) {
            System.out.println("warnings=" + result.getWarnings());
        }
    }
}
