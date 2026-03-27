using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenCvSharp.Extensions;

namespace LSS_prototype.Dicom_Module
{
    public partial class DicomManager
    {
        private readonly DicomDataset dataset;
        private readonly DicomDataset videoDataset;

        private const string SOPClassUID = "1.2.840.10008.5.1.4.1.1.77.1.4"; // VL Photographic Image Storage
        private const string SOPClassUIDVideo = "1.2.840.10008.5.1.4.1.1.7";  // Secondary Capture Image Storage
        
        private static readonly DicomTag GuaranteeTag = new DicomTag(0x0009, 0x0010); // 수동 dicom tag 값 
        // ─────────────────────────────────────────────
        // 생성자
        // ─────────────────────────────────────────────

        /// <summary>
        /// EMR 환자용: MWL 서버에서 받아온 기존 DicomDataset을 복사하여 초기화.
        /// AccessionNumber, StudyInstanceUID 등 서버 원본 태그를 최대한 유지한다.
        /// </summary>
        public DicomManager(string id, string serialnumber, DicomDataset sourceDataset)
        {
            dataset = new DicomDataset();
            videoDataset = new DicomDataset();  // dataset과 별도 객체 → SetVideo의 dataset.AddOrUpdate(videoDataset) 충돌 방지

            if (sourceDataset != null)
            {
                foreach (var item in sourceDataset)
                    dataset.AddOrUpdate(item);
            }
        }

        public DicomManager()
        {

        }

        /// <summary>
        /// LOCAL 환자용: 빈 데이터셋으로 초기화.
        /// </summary>
        public DicomManager(string id, string serialnumber)
        {
            dataset = new DicomDataset();
            videoDataset = new DicomDataset();
        }

        // ─────────────────────────────────────────────
        // 내부 헬퍼
        // ─────────────────────────────────────────────

        private static string NewUid()
        {
            return DicomUIDGenerator.GenerateDerivedFromUUID().UID;
        }

        /// <summary>
        /// dataset에 태그가 없을 경우에만 추가.
        /// EMR 환자의 경우 서버 원본 태그를 덮어쓰지 않기 위해 사용.
        /// </summary>
        private void AddIfNotExists<T>(DicomTag tag, T value)
        {
            if (!dataset.Contains(tag))
                dataset.Add(tag, value);
        }

        // ─────────────────────────────────────────────
        // 장비 정보
        // ─────────────────────────────────────────────

        private void SetGeneralEquipment()
        {
            AddIfNotExists(DicomTag.Manufacturer, "S-ONE BIO");
            AddIfNotExists(DicomTag.ManufacturerModelName, "LSS ICG_A1.0");
        }

        // ─────────────────────────────────────────────
        // 환자 / Study / Series / Instance 태그 설정
        // ─────────────────────────────────────────────

        public void SetPatient(string id, string name, string birthDate, string gender, string age, string guarantee)
        {
            AddIfNotExists(DicomTag.PatientID, id);
            AddIfNotExists(DicomTag.SpecificCharacterSet, "ISO_IR 149");
            AddIfNotExists(DicomTag.PatientName, name);
            AddIfNotExists(DicomTag.PatientBirthDate, birthDate);
            AddIfNotExists(DicomTag.PatientSex, gender);
            AddIfNotExists(DicomTag.PatientAge, age.PadLeft(3, '0') + "Y");
            AddIfNotExists(DicomTag.AdmittingDiagnosesDescription, "");
            AddIfNotExists(GuaranteeTag, guarantee); // 수동으로 주입된 상태
        }

        /// <summary>
        /// Study 태그 설정.
        /// EMR이면 기존 StudyInstanceUID 유지, 없으면 새로 생성.
        /// LOCAL이면 새로 생성.
        /// </summary>
        public void SetStudy(
            string studyID,
            string accessionNumber,
            string date,
            string time,
            string injection,
            string hospitalName,
            string description)
        {
            if (!dataset.Contains(DicomTag.StudyInstanceUID))
                dataset.AddOrUpdate(DicomTag.StudyInstanceUID, NewUid());

            AddIfNotExists(DicomTag.StudyID, studyID);
            AddIfNotExists(DicomTag.AccessionNumber, accessionNumber ?? "");
            dataset.AddOrUpdate(DicomTag.StudyDate, date);
            AddIfNotExists(DicomTag.ScheduledProcedureStepStartTime, injection ?? "");
            AddIfNotExists(DicomTag.StudyTime, time);
            AddIfNotExists(DicomTag.InstitutionName, hospitalName ?? "");
        }

        public void SetSeries(string seriesNumber, string bodyPart, string date, string time)
        {
            dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, NewUid());
            dataset.AddOrUpdate(DicomTag.SeriesNumber, seriesNumber);
            AddIfNotExists(DicomTag.BodyPartExamined, bodyPart ?? "");
            AddIfNotExists(DicomTag.SeriesDate, date);
            AddIfNotExists(DicomTag.SeriesTime, time);
            AddIfNotExists(DicomTag.Modality, "XC");
            AddIfNotExists(DicomTag.SmallestPixelValueInSeries, "0");
            AddIfNotExists(DicomTag.LargestPixelValueInSeries, "32767");
        }

        public void SetContent(string seriesNumber, string date, string time, string instanceNumber)
        {
            dataset.AddOrUpdate(DicomTag.SOPClassUID, SOPClassUID);
            dataset.AddOrUpdate(DicomTag.SOPInstanceUID, NewUid());
            AddIfNotExists(DicomTag.ContentDate, date);
            AddIfNotExists(DicomTag.ContentTime, time);
            AddIfNotExists(DicomTag.InstanceNumber, instanceNumber);
        }

        // ─────────────────────────────────────────────
        // Private 장비 파라미터 태그
        // ─────────────────────────────────────────────

        public void SetPrivateDataElement(double exposure, double gain, double gamma)
        {
            dataset.AddOrUpdate(new DicomTag(0x0009, 0x0010), exposure.ToString());
            dataset.AddOrUpdate(new DicomTag(0x0009, 0x0011), gain.ToString());
            dataset.AddOrUpdate(new DicomTag(0x0009, 0x0012), gamma.ToString());
        }

        // ─────────────────────────────────────────────
        // 동영상 전용 태그 설정
        // ─────────────────────────────────────────────

        public void SetContentVideo(string seriesNumber, string date, string time)
        {
            videoDataset.AddOrUpdate(DicomTag.SOPClassUID, SOPClassUIDVideo);
            videoDataset.AddOrUpdate(DicomTag.SOPInstanceUID, NewUid());
            videoDataset.AddOrUpdate(DicomTag.ContentDate, date);
            videoDataset.AddOrUpdate(DicomTag.ContentTime, time);
        }

        // ─────────────────────────────────────────────
        // 파일 저장
        // ─────────────────────────────────────────────

        public async Task<bool> SaveImageFile(string savePath, Bitmap bitmap)
        {
            if (string.IsNullOrWhiteSpace(savePath) || bitmap == null)
                return false;

            await Task.Run(() =>
            {
                SetGeneralEquipment();
                SetImage(bitmap);

                var file = new DicomFile(dataset);
                file.Save(savePath);
            });

            return true;
        }

        public bool SaveVideoFile(string savePath, string videoPath)
        {
            if (string.IsNullOrWhiteSpace(savePath) || string.IsNullOrWhiteSpace(videoPath))
                return false;

            SetGeneralEquipment();
            SetVideo(videoPath);

            var file = new DicomFile(dataset);
            file.Save(savePath);

            Console.WriteLine("DICOM 성공");
            return true;
        }

        // ─────────────────────────────────────────────
        // 픽셀 데이터 처리
        // ─────────────────────────────────────────────

        private void SetImage(Bitmap sourceBitmap)
        {
            using (Bitmap bitmap = GetValidImage(sourceBitmap))
            {
                byte[] pixels = GetPixels(bitmap);
                var buffer = new MemoryByteBuffer(pixels);

                dataset.AddOrUpdate(DicomTag.SamplesPerPixel, (ushort)3);
                dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value);
                dataset.AddOrUpdate(DicomTag.Rows, (ushort)bitmap.Height);
                dataset.AddOrUpdate(DicomTag.Columns, (ushort)bitmap.Width);
                dataset.AddOrUpdate(DicomTag.BitsAllocated, (ushort)8);
                dataset.AddOrUpdate(DicomTag.BitsStored, (ushort)8);
                dataset.AddOrUpdate(DicomTag.HighBit, (ushort)7);
                dataset.AddOrUpdate(DicomTag.PixelRepresentation, (ushort)0);
                dataset.AddOrUpdate(DicomTag.SmallestImagePixelValue, "0");
                dataset.AddOrUpdate(DicomTag.LargestImagePixelValue, "255");

                DicomPixelData dpData = DicomPixelData.Create(dataset, true);
                dpData.SamplesPerPixel = 3;
                dpData.PhotometricInterpretation = PhotometricInterpretation.Rgb;
                dpData.PlanarConfiguration = 0;
                dpData.AddFrame(buffer);
            }
        }

        private void SetVideo(string videoPath)
        {
            using (VideoCapture vCapture = new VideoCapture(videoPath))
            {
                int frameCnt = (int)vCapture.FrameCount;
                int fps = (int)vCapture.Fps;
                if (fps <= 0) fps = 30;
                int duration = frameCnt / fps;

                Console.WriteLine("Frame Count : {0}, FPS : {1}, Duration : {2}", frameCnt, fps, duration);
                Console.WriteLine("Frame Width : {0}, Height : {1}", vCapture.FrameWidth, vCapture.FrameHeight);

                videoDataset.AddOrUpdate(DicomTag.SamplesPerPixel, (ushort)3);
                videoDataset.AddOrUpdate(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value);
                videoDataset.AddOrUpdate(DicomTag.Rows, (ushort)vCapture.FrameHeight);
                videoDataset.AddOrUpdate(DicomTag.Columns, (ushort)vCapture.FrameWidth);
                videoDataset.AddOrUpdate(DicomTag.BitsAllocated, (ushort)8);
                videoDataset.AddOrUpdate(DicomTag.BitsStored, (ushort)8);
                videoDataset.AddOrUpdate(DicomTag.HighBit, (ushort)7);
                videoDataset.AddOrUpdate(DicomTag.PixelRepresentation, (ushort)0);
                videoDataset.AddOrUpdate(DicomTag.StartTrim, "1");
                videoDataset.AddOrUpdate(DicomTag.StopTrim, frameCnt.ToString());
                videoDataset.AddOrUpdate(DicomTag.CineRate, fps.ToString());
                videoDataset.AddOrUpdate(DicomTag.EffectiveDuration, duration.ToString());
                videoDataset.AddOrUpdate(DicomTag.NumberOfFrames, frameCnt.ToString());

                DicomPixelData dpData = DicomPixelData.Create(videoDataset, true);
                dpData.SamplesPerPixel = 3;
                dpData.PhotometricInterpretation = PhotometricInterpretation.Rgb;
                dpData.PlanarConfiguration = 0;

                using (Mat captureImg = new Mat())
                {
                    while (true)
                    {
                        vCapture.Read(captureImg);
                        if (captureImg.Empty())
                            break;

                        using (Bitmap bitmap = BitmapConverter.ToBitmap(captureImg))
                        using (Bitmap validBitmap = GetValidImage(bitmap))
                        {
                            byte[] pixels = GetPixels(validBitmap);
                            var buffer = new MemoryByteBuffer(pixels);
                            dpData.AddFrame(buffer);
                        }
                    }
                }

                dataset.AddOrUpdate(videoDataset);
            }
        }

        private Bitmap GetValidImage(Bitmap bitmap)
        {
            if (bitmap.PixelFormat == PixelFormat.Format24bppRgb)
                return (Bitmap)bitmap.Clone();

            Bitmap converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(converted))
            {
                g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
            }
            return converted;
        }

        private byte[] GetPixels(Bitmap image)
        {
            int rows = image.Height;
            int columns = image.Width;

            if (rows % 2 != 0 && columns % 2 != 0)
                --columns;

            BitmapData data = image.LockBits(
                new Rectangle(0, 0, columns, rows),
                ImageLockMode.ReadOnly,
                image.PixelFormat);

            IntPtr bmpData = data.Scan0;

            try
            {
                int stride = columns * 3;
                int size = rows * stride;
                byte[] pixelData = new byte[size];

                for (int i = 0; i < rows; ++i)
                {
                    Marshal.Copy(
                        new IntPtr(bmpData.ToInt64() + i * data.Stride),
                        pixelData,
                        i * stride,
                        stride);
                }

                // BGR -> RGB
                for (int i = 0; i < pixelData.Length; i += 3)
                {
                    byte temp = pixelData[i];
                    pixelData[i] = pixelData[i + 2];
                    pixelData[i + 2] = temp;
                }

                return pixelData;
            }
            finally
            {
                image.UnlockBits(data);
            }
        }
    }
}