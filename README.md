# ChunkDownloader
Downloader for files that are restricted by a college or school proxy

	This downloader has been created based on the fact that a large file however big it may be, can be downloaded in a restricted environment (where we are allowed to download files of size 50MB or less) by sending requests to the server that ask for chunks within the acceptable range (within the maximum download limit of the network say 50MB). Now these small chunks are downloaded individually and appended to the parent file to get the complete file.
	
# Working
	*	It uses a worker thread to perform a non-blocking download of the file. 
	*	The files are divided into chunks and their streams are requested.
	*	Using the streams we download the file in parts of 256, 512bytes or whichever is preferable
	* The data of the stream that was downloaded is now appended to the original parent file
