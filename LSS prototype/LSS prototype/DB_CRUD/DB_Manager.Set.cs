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
    }
}