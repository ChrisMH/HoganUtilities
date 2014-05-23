using System;
using System.Collections.Generic;
using System.Data.Common;
using NLog;
using Npgsql;

namespace Utility.Database.PostgreSql
{
    public class PgDbManager : IDbManager
    {
        public PgDbManager()
        {
        }

        public void Create()
        {
            Destroy();

            using (var conn = CreateDatabaseConnection())
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = string.Format("CREATE DATABASE \"{0}\"", Description.ConnectionInfo.DatabaseName);
                    if (Description is PgDbDescription && !string.IsNullOrEmpty(((PgDbDescription) Description).TemplateName))
                    {
                        cmd.CommandText += string.Format(" TEMPLATE \"{0}\"", ((PgDbDescription) Description).TemplateName);
                    }
                    cmd.ExecuteNonQuery();
                    LogManager.GetCurrentClassLogger().Info("Create: Created database '{0}'", Description.ConnectionInfo.DatabaseName);

                    if (!string.IsNullOrWhiteSpace(Description.ConnectionInfo.UserName) && !string.IsNullOrWhiteSpace(Description.ConnectionInfo.Password))
                    {
                        cmd.CommandText = string.Format("SELECT COUNT(*) FROM pg_catalog.pg_user WHERE usename='{0}'", Description.ConnectionInfo.UserName);
                        if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
                        {
                            cmd.CommandText = string.Format("CREATE ROLE \"{0}\" LOGIN ENCRYPTED PASSWORD '{1}' NOINHERIT", Description.ConnectionInfo.UserName,
                                Description.ConnectionInfo.Password);
                            cmd.ExecuteNonQuery();
                            LogManager.GetCurrentClassLogger().Info("Create: Created role '{0}'", Description.ConnectionInfo.UserName);
                        }
                    }
                }
            }

            using (var conn = CreateContentConnection())
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    foreach (var schemaDefinition in Description.Schemas)
                    {
                        try
                        {
                            cmd.CommandText = schemaDefinition.Load();
                            cmd.ExecuteNonQuery();
                            LogManager.GetCurrentClassLogger().Info("Create: Executed create from {0} script '{1}'", schemaDefinition.ScriptType, schemaDefinition.ScriptValue);
                        }
                        catch (Exception ex)
                        {
                            if (schemaDefinition.ScriptType == ScriptType.File || schemaDefinition.ScriptType == ScriptType.Resource)
                            {
                                throw new Exception(
                                    string.Format("Create: Error creating from {0} script '{1}': {2}", schemaDefinition.ScriptType, schemaDefinition.ScriptValue, ex.Message), ex);
                            }
                            throw new Exception(string.Format("Create: Error creating from {0} script : {1}", schemaDefinition.ScriptType, ex.Message), ex);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(Description.ConnectionInfo.UserName))
                    {
                        cmd.CommandText = "SELECT nspname FROM pg_catalog.pg_namespace " +
                                          "WHERE nspname NOT LIKE 'pg_%' " +
                                          "AND nspname NOT IN ('information_schema')";
                        var schemas = new List<string>();
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                schemas.Add(rdr.GetString(0));
                            }
                        }

                        if (schemas.Count > 0)
                        {
                            cmd.CommandText = "";
                            foreach (var schema in schemas)
                            {
                                cmd.CommandText += string.Format("GRANT ALL ON SCHEMA \"{0}\" TO \"{1}\";" +
                                                                 "GRANT ALL ON ALL TABLES IN SCHEMA \"{0}\" TO \"{1}\";" +
                                                                 "GRANT ALL ON ALL SEQUENCES IN SCHEMA \"{0}\" TO \"{1}\";" +
                                                                 "GRANT ALL ON ALL FUNCTIONS IN SCHEMA \"{0}\" TO \"{1}\";",
                                    schema, Description.ConnectionInfo.UserName);
                            }

                            cmd.ExecuteNonQuery();

                            LogManager.GetCurrentClassLogger().Info("Create: Granted permissions to '{0}'", Description.ConnectionInfo.UserName);
                        }
                    }
                }
            }
        }

        public void Destroy()
        {
            VerifyProperties();

            NpgsqlConnection.ClearAllPools();

            using (var conn = CreateDatabaseConnection())
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = string.Format("DROP DATABASE IF EXISTS \"{0}\"", Description.ConnectionInfo.DatabaseName);
                    cmd.ExecuteNonQuery();
                    LogManager.GetCurrentClassLogger().Info("Destroy: Dropped database '{0}'", Description.ConnectionInfo.DatabaseName);

                    if (!string.IsNullOrWhiteSpace(Description.ConnectionInfo.UserName) &&
                        !Description.ConnectionInfo.UserName.Equals(Description.Superuser.UserName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Delete the role if it is not in use by any databases
                        cmd.CommandText = string.Format("SELECT COUNT(*) FROM pg_catalog.pg_shdepend sd " +
                                                        "JOIN pg_catalog.pg_roles r ON r.oid = sd.refobjid " +
                                                        "WHERE r.rolname='{0}' ", Description.ConnectionInfo.UserName);

                        if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
                        {
                            cmd.CommandText = string.Format("DROP ROLE IF EXISTS \"{0}\"", Description.ConnectionInfo.UserName);
                            cmd.ExecuteNonQuery();
                            LogManager.GetCurrentClassLogger().Info("Destroy: Dropped role '{0}'", Description.ConnectionInfo.UserName);
                        }
                    }
                }
            }
        }

        public void Seed()
        {
            VerifyProperties();

            using (var conn = CreateContentConnection())
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    foreach (var seedDefinition in Description.Seeds)
                    {
                        try
                        {
                            cmd.CommandText = seedDefinition.Load();
                            cmd.ExecuteNonQuery();
                            LogManager.GetCurrentClassLogger().Info("Seed: Executed seed from {0} script '{1}'", seedDefinition.ScriptType, seedDefinition.ScriptValue);
                        }
                        catch (Exception ex)
                        {
                            if (seedDefinition.ScriptType == ScriptType.File || seedDefinition.ScriptType == ScriptType.Resource)
                            {
                                throw new Exception(
                                    string.Format("Seed: Error seeding from {0} script '{1}': {2}", seedDefinition.ScriptType, seedDefinition.ScriptValue, ex.Message), ex);
                            }
                            throw new Exception(string.Format("Seed: Error seeding from {0} script : {1}", seedDefinition.ScriptType, ex.Message), ex);
                        }
                    }
                }
            }
        }

        public IDbDescription Description { get; set; }


        internal DbConnection CreateDatabaseConnection()
        {
            VerifyProperties();

            var csBuilder = new DbConnectionStringBuilder {ConnectionString = Description.ConnectionInfo.ConnectionString};
            csBuilder[PgDbConnectionInfo.DatabaseNameKey] = ((PgSuperuser) Description.Superuser).DatabaseName;
            csBuilder[PgDbConnectionInfo.UserNameKey] = Description.Superuser.UserName;
            csBuilder[PgDbConnectionInfo.PasswordKey] = Description.Superuser.Password;

            var conn = new NpgsqlConnection(csBuilder.ConnectionString);

            return conn;
        }

        internal DbConnection CreateContentConnection()
        {
            VerifyProperties();

            var csBuilder = new DbConnectionStringBuilder {ConnectionString = Description.ConnectionInfo.ConnectionString};
            csBuilder[PgDbConnectionInfo.UserNameKey] = Description.Superuser.UserName;
            csBuilder[PgDbConnectionInfo.PasswordKey] = Description.Superuser.Password;

            var conn = new NpgsqlConnection(csBuilder.ConnectionString);

            return conn;
        }

        protected void VerifyProperties()
        {
            if (Description == null) throw new ArgumentException("Description is null", "Description");
            if (Description.Superuser == null) throw new ArgumentException("Description.Superuser is null", "Description.Superuser");
            if (Description.ConnectionInfo == null) throw new ArgumentException("Description.ConnectionInfo is null", "Description.ConnectionInfo");
            if (string.IsNullOrEmpty(Description.ConnectionInfo.ConnectionString))
                throw new ArgumentException("Connection information is missing a connection string", "Description.ConnectionInfo.ConnectionString");

            if (Description.ConnectionInfo.GetType() != typeof (PgDbConnectionInfo))
            {
                Description.ConnectionInfo = new PgDbConnectionInfo
                {
                    ConnectionString = Description.ConnectionInfo.ConnectionString,
                    Provider = Description.ConnectionInfo.Provider
                };
            }
        }
    }
}