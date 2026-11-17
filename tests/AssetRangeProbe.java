package com.unlim8ted.celeste;

import java.util.Arrays;

public final class AssetRangeProbe {
    private static void check(String text, long size, long... expected) {
        long[] actual = LocalAssetServer.parseRange(text, size);
        if (!Arrays.equals(actual, expected.length == 0 ? null : expected))
            throw new AssertionError(text + ": " + Arrays.toString(actual));
    }

    public static void main(String[] args) {
        check("bytes=0-0", 100, 0, 0);
        check("bytes=10-19", 100, 10, 19);
        check("bytes=99-999", 100, 99, 99);
        check("bytes=90-", 100, 90, 99);
        check("bytes=-10", 100, 90, 99);
        check("bytes=-999", 100, 0, 99);
        check("bytes=0-0", 0);
        check("bytes=100-", 100);
        check("bytes=20-10", 100);
        check("bytes=-0", 100);
        check("bytes=-", 100);
        check("bytes=0-1,3-4", 100);
        check("bytes=0-99999999999999999999", 100);
        check("bytes=99999999999999999999-", 100);
        check("bytes=+1-2", 100);
        check("items=0-1", 100);
        System.out.println("PASS: asset range bounds, suffixes, EOF, and invalid requests");
    }
}
