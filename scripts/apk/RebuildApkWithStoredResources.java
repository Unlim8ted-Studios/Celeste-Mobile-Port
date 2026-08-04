import java.io.BufferedInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Enumeration;
import java.util.HashSet;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Set;
import java.util.zip.CRC32;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;
import java.util.zip.ZipOutputStream;

public class RebuildApkWithStoredResources {
    public static void main(String[] args) throws Exception {
        if (args.length != 3) {
            throw new IllegalArgumentException("usage: <source.apk> <workspace-root> <out.apk>");
        }

        File sourceApk = new File(args[0]);
        Path root = new File(args[1]).toPath();
        File outApk = new File(args[2]);

        try (ZipFile source = new ZipFile(sourceApk);
             ZipOutputStream out = new ZipOutputStream(new FileOutputStream(outApk))) {
            byte[] buffer = new byte[1024 * 1024];
            Set<String> written = new HashSet<>();
            Enumeration<? extends ZipEntry> entries = source.entries();
            while (entries.hasMoreElements()) {
                ZipEntry sourceEntry = entries.nextElement();
                if (sourceEntry.isDirectory()) {
                    continue;
                }

                String name = sourceEntry.getName();
                if (name.startsWith("META-INF/") && (name.endsWith(".SF") || name.endsWith(".RSA") || name.endsWith(".DSA") || name.endsWith("MANIFEST.MF"))) {
                    continue;
                }

                File file = fileForEntry(root, replacementEntryName(name));
                if (!file.isFile()) {
                    throw new IllegalStateException("Missing file for APK entry: " + name);
                }

                ZipEntry newEntry = new ZipEntry(name);
                newEntry.setTime(sourceEntry.getTime());
                byte[] patchedBytes = null;
                if ("resources.arsc".equals(name)) {
                    patchedBytes = patchResourcesArsc(Files.readAllBytes(file.toPath()));
                    newEntry.setMethod(ZipEntry.STORED);
                    newEntry.setSize(patchedBytes.length);
                    newEntry.setCompressedSize(patchedBytes.length);
                    newEntry.setCrc(crc32(patchedBytes));
                } else {
                    newEntry.setMethod(ZipEntry.DEFLATED);
                }

                out.putNextEntry(newEntry);
                if (patchedBytes != null) {
                    out.write(patchedBytes);
                } else {
                    try (BufferedInputStream in = new BufferedInputStream(new FileInputStream(file))) {
                        int read;
                        while ((read = in.read(buffer)) != -1) {
                            out.write(buffer, 0, read);
                        }
                    }
                }
                out.closeEntry();
                written.add(name);
            }

            Map<String, String> aliases = new LinkedHashMap<>();
            aliases.put("res/gR.xml", "res/gR_1.xml");
            aliases.put("res/qz.xml", "res/qz_1.xml");
            aliases.put("res/tL.xml", "res/tL_1.xml");
            aliases.put("res/bb.xml", "res/bb_1.xml");
            aliases.put("res/hq.xml", "res/hq_1.xml");
            aliases.put("res/yg.9.png", "res/yg.9_1.png");
            aliases.put("res/gt.9.png", "res/gt.9_1.png");
            aliases.put("res/9n.9.png", "res/9n.9_1.png");
            aliases.put("res/ar.png", "res/ar_1.png");
            aliases.put("res/x3.9.png", "res/x3.9_1.png");

            for (Map.Entry<String, String> alias : aliases.entrySet()) {
                if (written.contains(alias.getKey())) {
                    continue;
                }
                File file = root.resolve(alias.getValue().replace('/', File.separatorChar)).toFile();
                if (!file.isFile()) {
                    throw new IllegalStateException("Missing file for APK alias: " + alias.getValue());
                }
                ZipEntry newEntry = new ZipEntry(alias.getKey());
                newEntry.setMethod(ZipEntry.DEFLATED);
                out.putNextEntry(newEntry);
                try (BufferedInputStream in = new BufferedInputStream(new FileInputStream(file))) {
                    int read;
                    while ((read = in.read(buffer)) != -1) {
                        out.write(buffer, 0, read);
                    }
                }
                out.closeEntry();
                written.add(alias.getKey());
            }

            addExtraFile(root, out, written, "assets/www/Mods/AndroidPort.zip", buffer);
            addExtraDirectory(root, out, written, "assets/www/_framework", buffer);
            addExtraDirectory(root, out, written, "assets/www/celeste", buffer);
        }
    }

    private static void addExtraDirectory(Path root, ZipOutputStream out, Set<String> written, String name, byte[] buffer) throws Exception {
        Path dir = root.resolve(name.replace('/', File.separatorChar));
        if (!Files.isDirectory(dir)) {
            throw new IllegalStateException("Missing extra APK directory: " + name);
        }
        try (var files = Files.walk(dir)) {
            for (Path filePath : (Iterable<Path>) files.filter(Files::isRegularFile)::iterator) {
                String relative = root.relativize(filePath).toString().replace(File.separatorChar, '/');
                addExtraFile(root, out, written, relative, buffer);
            }
        }
    }

    private static void addExtraFile(Path root, ZipOutputStream out, Set<String> written, String name, byte[] buffer) throws Exception {
        if (written.contains(name)) {
            return;
        }
        File file = root.resolve(name.replace('/', File.separatorChar)).toFile();
        if (!file.isFile()) {
            throw new IllegalStateException("Missing extra APK file: " + name);
        }
        ZipEntry newEntry = new ZipEntry(name);
        newEntry.setMethod(ZipEntry.DEFLATED);
        out.putNextEntry(newEntry);
        try (BufferedInputStream in = new BufferedInputStream(new FileInputStream(file))) {
            int read;
            while ((read = in.read(buffer)) != -1) {
                out.write(buffer, 0, read);
            }
        }
        out.closeEntry();
        written.add(name);
    }

    private static String replacementEntryName(String name) {
        Map<String, String> replacements = new HashMap<>();
        replacements.put("res/gR.xml", "res/gR_1.xml");
        replacements.put("res/qz.xml", "res/qz_1.xml");
        replacements.put("res/tL.xml", "res/tL_1.xml");
        replacements.put("res/bb.xml", "res/bb_1.xml");
        replacements.put("res/hq.xml", "res/hq_1.xml");
        replacements.put("res/yg.9.png", "res/yg.9_1.png");
        replacements.put("res/gt.9.png", "res/gt.9_1.png");
        replacements.put("res/9n.9.png", "res/9n.9_1.png");
        replacements.put("res/ar.png", "res/ar_1.png");
        replacements.put("res/x3.9.png", "res/x3.9_1.png");
        return replacements.getOrDefault(name, name);
    }

    private static long crc32(File file, byte[] buffer) throws Exception {
        CRC32 crc = new CRC32();
        try (BufferedInputStream in = new BufferedInputStream(new FileInputStream(file))) {
            int read;
            while ((read = in.read(buffer)) != -1) {
                crc.update(buffer, 0, read);
            }
        }
        return crc.getValue();
    }

    private static long crc32(byte[] bytes) {
        CRC32 crc = new CRC32();
        crc.update(bytes, 0, bytes.length);
        return crc.getValue();
    }

    private static File fileForEntry(Path root, String name) {
        if ("res/pu.png".equals(name)) {
            File logo = root.resolve("logo-splash.png").toFile();
            if (logo.isFile()) {
                return logo;
            }
        }
        return root.resolve(name.replace('/', File.separatorChar)).toFile();
    }

    private static byte[] patchResourcesArsc(byte[] bytes) {
        byte[] oldId = new byte[] { 0x5f, 0x00, 0x07, 0x7f };
        byte[] newId = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        int matches = 0;
        for (int i = 0; i <= bytes.length - oldId.length; i++) {
            if (bytes[i] == oldId[0] && bytes[i + 1] == oldId[1] && bytes[i + 2] == oldId[2] && bytes[i + 3] == oldId[3]) {
                bytes[i] = newId[0];
                bytes[i + 1] = newId[1];
                bytes[i + 2] = newId[2];
                bytes[i + 3] = newId[3];
                matches++;
            }
        }
        if (matches != 1) {
            throw new IllegalStateException("Expected exactly one splash icon reference, patched " + matches);
        }
        patchSimpleResourceValue(bytes, 0x05, 0x2b, 0xff000000);
        return bytes;
    }

    private static void patchSimpleResourceValue(byte[] bytes, int targetTypeId, int targetEntryId, int newValue) {
        int offset = 12;
        boolean patched = false;
        while (offset + 8 <= bytes.length) {
            int type = u16(bytes, offset);
            int headerSize = u16(bytes, offset + 2);
            int size = u32(bytes, offset + 4);
            if (type == 0x0200) {
                int child = offset + headerSize;
                int end = offset + size;
                while (child + 8 <= end) {
                    int childType = u16(bytes, child);
                    int childHeaderSize = u16(bytes, child + 2);
                    int childSize = u32(bytes, child + 4);
                    if (childType == 0x0201 && (bytes[child + 8] & 0xff) == targetTypeId) {
                        int entryCount = u32(bytes, child + 12);
                        int entriesStart = u32(bytes, child + 16);
                        if (targetEntryId < entryCount) {
                            int entryOffset = u32(bytes, child + childHeaderSize + targetEntryId * 4);
                            if (entryOffset != 0xffffffff) {
                                int entry = child + entriesStart + entryOffset;
                                int flags = u16(bytes, entry + 2);
                                if ((flags & 0x0001) == 0) {
                                    int value = entry + 8;
                                    writeU32(bytes, value + 4, newValue);
                                    patched = true;
                                }
                            }
                        }
                    }
                    if (childSize <= 0) break;
                    child += childSize;
                }
            }
            if (size <= 0) break;
            offset += size;
        }
        if (!patched) {
            throw new IllegalStateException("Failed to patch resource type " + targetTypeId + " entry " + targetEntryId);
        }
    }

    private static int u16(byte[] bytes, int offset) {
        return (bytes[offset] & 0xff) | ((bytes[offset + 1] & 0xff) << 8);
    }

    private static int u32(byte[] bytes, int offset) {
        return (bytes[offset] & 0xff) | ((bytes[offset + 1] & 0xff) << 8) | ((bytes[offset + 2] & 0xff) << 16) | ((bytes[offset + 3] & 0xff) << 24);
    }

    private static void writeU32(byte[] bytes, int offset, int value) {
        bytes[offset] = (byte) (value & 0xff);
        bytes[offset + 1] = (byte) ((value >> 8) & 0xff);
        bytes[offset + 2] = (byte) ((value >> 16) & 0xff);
        bytes[offset + 3] = (byte) ((value >> 24) & 0xff);
    }
}
