using System;
using System.IO;
using System.Reflection;

namespace watchCode.helpers
{
    public static class IoHelper
    {
        public static bool EnsureDirExists(string absoluteSnapshotDirectoryPath)
        {
            try
            {
                var dirInfo = new DirectoryInfo(absoluteSnapshotDirectoryPath);

                if (dirInfo.Exists == false)
                {
                    Directory.CreateDirectory(dirInfo.FullName);
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not opne/create dir: {absoluteSnapshotDirectoryPath}, error: {e.Message}");
                return false;
            }

            return true;
        }


        public static bool CheckFileExists(string absoluteFilePath, bool reportErrorIfNotExists)
        {
            try
            {
                var fileInfo = new FileInfo(absoluteFilePath);

                if (fileInfo.Exists == false)
                {
                    if (reportErrorIfNotExists)
                        Logger.Error($"file does not exists, path: {absoluteFilePath}");

                    return false;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"could access file at: {absoluteFilePath}, error: {e.Message}");
                return false;
            }

            return true;
        }

        //from https://stackoverflow.com/questions/703281/getting-path-relative-to-the-current-working-directory
        //modified
        public static string GetRelativePath(string absoluteFilePath, string absoluteFolderPath)
        {
            //see https://github.com/dotnet/corefx/issues/1745
            //use C: at front to be ablue to use this with dotnet core 1.x

            //TODO not sure if this always works correctly...

            absoluteFilePath = "C:" + absoluteFilePath;
            absoluteFolderPath = "C:" + absoluteFolderPath;
            absoluteFilePath = absoluteFilePath.Replace(Path.DirectorySeparatorChar, '\\');
            absoluteFolderPath = absoluteFolderPath.Replace(Path.DirectorySeparatorChar, '\\');

            var pathUri = new Uri(absoluteFilePath);
            // Folders must end in a slash
            if (!absoluteFolderPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                absoluteFolderPath += Path.DirectorySeparatorChar;
            }
            var folderUri = new Uri(absoluteFolderPath);

            var relativePath = Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString()
                .Replace('/', Path.DirectorySeparatorChar));

            return relativePath;
        }

        //from https://stackoverflow.com/questions/5404267/streamreader-and-seeking/17457085#17457085
        public static long GetActualPosition(StreamReader reader)
        {
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.DeclaredOnly |
                                                   System.Reflection.BindingFlags.NonPublic |
                                                   System.Reflection.BindingFlags.Instance |
                                                   System.Reflection.BindingFlags.GetField;

            // The current buffer of decoded characters
            //reader.GetType().InvokeMember("charBuffer", flags, null, reader, null);

            var m = reader.GetType().GetField("charBuffer", flags);
            char[] charBuffer = (char[]) m.GetValue(reader);

            // The index of the next char to be read from charBuffer
            //reader.GetType().InvokeMember("charPos", flags, null, reader, null);
            int charPos = (int) reader.GetType().GetField("charPos", flags).GetValue(reader);

            // The number of decoded chars presently used in charBuffer
            //reader.GetType().InvokeMember("charLen", flags, null, reader, null);
            int charLen = (int) reader.GetType().GetField("charLen", flags).GetValue(reader);

            // The current buffer of read bytes (byteBuffer.Length = 1024; this is critical).
            //reader.GetType().InvokeMember("byteBuffer", flags, null, reader, null);
            byte[] byteBuffer = (byte[]) reader.GetType().GetField("byteBuffer", flags).GetValue(reader);

            // The number of bytes read while advancing reader.BaseStream.Position to (re)fill charBuffer
            //reader.GetType().InvokeMember("byteLen", flags, null, reader, null);
            int byteLen = (int) reader.GetType().GetField("byteLen", flags).GetValue(reader);

            // The number of bytes the remaining chars use in the original encoding.
            int numBytesLeft = reader.CurrentEncoding.GetByteCount(charBuffer, charPos, charLen - charPos);

            // For variable-byte encodings, deal with partial chars at the end of the buffer
            int numFragments = 0;
            if (byteLen > 0 && !reader.CurrentEncoding.IsSingleByte)
            {
                if (reader.CurrentEncoding.CodePage == 65001) // UTF-8
                {
                    byte byteCountMask = 0;
                    while ((byteBuffer[byteLen - numFragments - 1] >> 6) == 2
                    ) // if the byte is "10xx xxxx", it's a continuation-byte
                        byteCountMask |= (byte) (1 << ++numFragments); // count bytes & build the "complete char" mask
                    if ((byteBuffer[byteLen - numFragments - 1] >> 6) == 3
                    ) // if the byte is "11xx xxxx", it starts a multi-byte char.
                        byteCountMask |= (byte) (1 << ++numFragments); // count bytes & build the "complete char" mask
                    // see if we found as many bytes as the leading-byte says to expect
                    if (numFragments > 1 && ((byteBuffer[byteLen - numFragments] >> 7 - numFragments) == byteCountMask))
                        numFragments = 0; // no partial-char in the byte-buffer to account for
                }
                else if (reader.CurrentEncoding.CodePage == 1200) // UTF-16LE
                {
                    if (byteBuffer[byteLen - 1] >= 0xd8) // high-surrogate
                        numFragments = 2; // account for the partial character
                }
                else if (reader.CurrentEncoding.CodePage == 1201) // UTF-16BE
                {
                    if (byteBuffer[byteLen - 2] >= 0xd8) // high-surrogate
                        numFragments = 2; // account for the partial character
                }
            }
            return reader.BaseStream.Position - numBytesLeft - numFragments;
        }
    }
}