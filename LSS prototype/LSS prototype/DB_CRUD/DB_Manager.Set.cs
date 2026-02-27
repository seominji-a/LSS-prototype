using LSS_prototype.User_Page;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.DB_CRUD
{
    public partial class DB_Manager
    {
        #region [ 카메라 기본값 로드 담당부 ]
        public DefaultModel GetDefaultSet()
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_DEFAULT, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new DefaultModel
                            {
                                ExposureTime = Convert.ToDouble(reader["EXPOSURE_TIME"]),
                                Gain         = Convert.ToDouble(reader["GAIN"]),
                                Gamma        = Convert.ToDouble(reader["GAMMA"]),
                                Focus        = Convert.ToDouble(reader["FOCUS"]),
                                Iris         = Convert.ToDouble(reader["IRIS"]),
                                Zoom         = Convert.ToInt32(reader["ZOOM"]),
                                Filter       = Convert.ToInt32(reader["FILTER"])
                            };
                        }
                    }
                }
            }
            return null;
        }
        #endregion

        #region [ 카메라 기본값 수정 담당부 ]
        public bool UpdateDefaultSet(DefaultModel data)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.UPDATE_DEFAULT, conn))
                {
                    cmd.Parameters.AddWithValue("@ExposureTime", data.ExposureTime);
                    cmd.Parameters.AddWithValue("@Gain",         data.Gain);
                    cmd.Parameters.AddWithValue("@Gamma",        data.Gamma);
                    cmd.Parameters.AddWithValue("@Focus",        data.Focus);
                    cmd.Parameters.AddWithValue("@Iris",         data.Iris);
                    cmd.Parameters.AddWithValue("@Zoom",         data.Zoom);
                    cmd.Parameters.AddWithValue("@Filter",       data.Filter);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        #endregion

        #region [ PACS 설정 로드 담당부 ]
        public SettingModel GetPacsSet()
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_PACS, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new SettingModel
                            {
                                HospitalName = reader["HOSPITAL_NAME"].ToString(),
                                CStoreAET = reader["CSTORE_AET"].ToString(),
                                CStoreIP = reader["CSTORE_IP"].ToString(),
                                CStorePort = Convert.ToInt32(reader["CSTORE_PORT"]),
                                CStoreMyAET = reader["CSTORE_MY_AET"].ToString(),
                                MwlAET = reader["MWL_AET"].ToString(),
                                MwlIP = reader["MWL_IP"].ToString(),
                                MwlPort = Convert.ToInt32(reader["MWL_PORT"]),
                                MwlMyAET = reader["MWL_MY_AET"].ToString()
                            };
                        }
                    }
                }
            }
            return null;
        }
        #endregion

        #region [ 병원 이름 수정 담당부 ] 
        public bool UpdateHospitalName(string hospitalName)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.UPDATE_HOSPITAL, conn))
                {
                    cmd.Parameters.AddWithValue("@HospitalName", hospitalName);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        #endregion

        #region [ C-STORE 설정 수정 담당부 ]
        public bool UpdateCStore(SettingModel data)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.UPDATE_CSTORE, conn))
                {
                    cmd.Parameters.AddWithValue("@CStoreAET", data.CStoreAET);
                    cmd.Parameters.AddWithValue("@CStoreIP", data.CStoreIP);
                    cmd.Parameters.AddWithValue("@CStorePort", data.CStorePort);
                    cmd.Parameters.AddWithValue("@CStoreMyAET", data.CStoreMyAET);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        #endregion

        #region [ MWL 설정 수정 담당부 ]
        public bool UpdateMwl(SettingModel data)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.UPDATE_MWL, conn))
                {
                    cmd.Parameters.AddWithValue("@MwlAET", data.MwlAET);
                    cmd.Parameters.AddWithValue("@MwlIP", data.MwlIP);
                    cmd.Parameters.AddWithValue("@MwlPort", data.MwlPort);
                    cmd.Parameters.AddWithValue("@MwlMyAET", data.MwlMyAET);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        #endregion
    }
}