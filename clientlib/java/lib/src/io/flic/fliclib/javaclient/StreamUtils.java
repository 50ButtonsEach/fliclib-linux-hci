package io.flic.fliclib.javaclient;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;

class StreamUtils {
    public static boolean getBoolean(InputStream stream) throws IOException {
        return stream.read() != 0;
    }
    public static int getUInt8(InputStream stream) throws IOException {
        return stream.read();
    }

    public static int getInt8(InputStream stream) throws IOException {
        return (byte)stream.read();
    }

    public static int getUInt16(InputStream stream) throws IOException {
        return stream.read() | (stream.read() << 8);
    }

    public static int getInt16(InputStream stream) throws IOException {
        return (short)getUInt16(stream);
    }

    public static int getInt32(InputStream stream) throws IOException {
        return stream.read() | (stream.read() << 8) | (stream.read() << 16) | (stream.read() << 24);
    }

    public static Bdaddr getBdaddr(InputStream stream) throws IOException {
        return new Bdaddr(stream);
    }

    public static void writeEnum(OutputStream stream, Enum<?> enumValue) throws IOException {
        stream.write(enumValue.ordinal());
    }

    public static void writeInt8(OutputStream stream, int v) throws IOException {
        stream.write(v);
    }

    public static void writeInt16(OutputStream stream, int v) throws IOException {
        stream.write(v & 0xff);
        stream.write(v >> 8);
    }

    public static void writeInt32(OutputStream stream, int v) throws IOException {
        writeInt16(stream, v);
        writeInt16(stream, v >> 16);
    }

    public static void writeBdaddr(OutputStream stream, Bdaddr addr) throws IOException {
        stream.write(addr.getBytes());
    }
}
