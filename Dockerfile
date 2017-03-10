FROM microsoft/dotnet

ADD / /build/module

RUN \
		set -ex \
	&& \
		apt-get update && apt-get install -y \
			build-essential \
			libcurl4-openssl-dev \
			git \
			cmake \
			libssl-dev \
			valgrind \
			uuid-dev \
			libglib2.0-dev \
	&& \
		git clone --no-checkout https://github.com/Azure/azure-iot-gateway-sdk /build/gateway \
	&& \
        git -C /build/gateway checkout 2017-03-06 && git -C /build/gateway submodule update --recursive --init \
	&& \
        /build/module/bld/build.sh -C Release -i /build/gateway -o /gateway \
	&& \
		ldconfig /gateway/Release \
	&& \
		rm -rf /build

WORKDIR /gateway/Release
ENTRYPOINT ["sample_gateway"]
