namespace Host.Patients.Loaders
{
    /// Loads a 2D image series (axial slice stack) from disk into an in-memory
    /// ImageSeries. Concrete implementations: PngImageSeriesLoader (today),
    /// DicomImageSeriesLoader (Phase F via fo-dicom).
    public interface IImageSeriesLoader
    {
        /// Lowercased format token in DicomSeriesRef.Format ("png_sequence", "dicom").
        string Format { get; }

        /// Load + return the series, or null on failure (logs detail).
        ImageSeries Load(string directoryPath, string id, string modality);
    }
}
